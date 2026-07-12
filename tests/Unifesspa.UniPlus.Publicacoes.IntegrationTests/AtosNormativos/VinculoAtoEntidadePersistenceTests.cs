namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.AtosNormativos;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

/// <summary>
/// Integração do vínculo ato ↔ entidade e da vaga de linhagem única por objeto contra
/// Postgres real: unicidade do trio, append-only imposto por trigger, e as duas travas
/// que impedem o <c>INSERT</c> cru de forjar uma vaga ou de omiti-la (ADR-0107).
/// </summary>
[Collection(PublicacoesDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "INSERT cru escrito no próprio teste, com identificadores gerados nele — é o que se quer exercitar: a trava do banco contra escrita fora do agregado.")]
public sealed class VinculoAtoEntidadePersistenceTests
{
    private const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string RestrictViolation = "23001";
    private const string UniqueViolation = "23505";
    private const string ForeignKeyViolation = "23503";
    private const string TipoUnico = "EDITAL_ABERTURA";
    private static readonly DateOnly Publicacao = new(2026, 3, 13);
    private static readonly DateTimeOffset Registro = new(2026, 3, 13, 19, 0, 0, TimeSpan.Zero);

    private readonly PublicacoesDbFixture _fixture;

    public VinculoAtoEntidadePersistenceTests(PublicacoesDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "O ato persiste com os seus vínculos, e o vínculo aponta para ele")]
    public async Task Insert_PersisteVinculos()
    {
        Guid processo = Guid.CreateVersion7();
        Guid chamada = Guid.CreateVersion7();
        AtoNormativo ato = Novo(unicoPorObjeto: false, vinculos: [("PROCESSO_SELETIVO", processo), ("CHAMADA", chamada)]);

        await Gravar(ato);

        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(userId: null);
        List<VinculoAtoEntidade> vinculos = await ctx.VinculosAtoEntidade
            .Where(v => v.AtoId == ato.Id)
            .ToListAsync();

        vinculos.Should().HaveCount(2);
        vinculos.Select(v => v.EntidadeId).Should().BeEquivalentTo([processo, chamada]);
    }

    [Fact(DisplayName = "Um ato sem vínculo persiste — há atos que não tratam de objeto algum")]
    public async Task Insert_SemVinculo_Persiste()
    {
        AtoNormativo ato = Novo(unicoPorObjeto: true);

        await Gravar(ato);

        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(userId: null);
        (await ctx.AtosNormativos.AnyAsync(a => a.Id == ato.Id)).Should().BeTrue();
        (await ctx.VinculosAtoEntidade.AnyAsync(v => v.AtoId == ato.Id)).Should().BeFalse();
    }

    [Fact(DisplayName = "UPDATE cru no vínculo é bloqueado pelo trigger append-only")]
    public async Task Update_Vinculo_Bloqueado()
    {
        AtoNormativo ato = Novo(unicoPorObjeto: false, vinculos: [("PROCESSO_SELETIVO", Guid.CreateVersion7())]);
        await Gravar(ato);

        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(() => ExecutarCruAsync(
            $"UPDATE publicacoes.vinculo_ato_entidade SET entidade_id = '{Guid.CreateVersion7()}' WHERE ato_id = '{ato.Id}'"));

        erro.SqlState.Should().Be(RestrictViolation);
    }

    [Fact(DisplayName = "DELETE cru no vínculo é bloqueado pelo trigger append-only")]
    public async Task Delete_Vinculo_Bloqueado()
    {
        AtoNormativo ato = Novo(unicoPorObjeto: false, vinculos: [("PROCESSO_SELETIVO", Guid.CreateVersion7())]);
        await Gravar(ato);

        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(() => ExecutarCruAsync(
            $"DELETE FROM publicacoes.vinculo_ato_entidade WHERE ato_id = '{ato.Id}'"));

        erro.SqlState.Should().Be(RestrictViolation);
    }

    [Fact(DisplayName = "O mesmo objeto vinculado duas vezes ao mesmo ato colide no índice único do trio")]
    public async Task Insert_TrioRepetido_Colide()
    {
        Guid processo = Guid.CreateVersion7();
        AtoNormativo ato = Novo(unicoPorObjeto: false, vinculos: [("PROCESSO_SELETIVO", processo)]);
        await Gravar(ato);

        // O agregado e o validator já recusam antes; aqui prova-se a trava do banco.
        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(() => ExecutarCruAsync(
            $"""
            INSERT INTO publicacoes.vinculo_ato_entidade (id, ato_id, entidade_tipo, entidade_id)
            VALUES ('{Guid.CreateVersion7()}', '{ato.Id}', 'PROCESSO_SELETIVO', '{processo}')
            """));

        erro.SqlState.Should().Be(UniqueViolation);
    }

    [Fact(DisplayName = "Vínculo órfão — sem ato — é recusado pela chave estrangeira")]
    public async Task Insert_VinculoOrfao_Recusado()
    {
        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(() => ExecutarCruAsync(
            $"""
            INSERT INTO publicacoes.vinculo_ato_entidade (id, ato_id, entidade_tipo, entidade_id)
            VALUES ('{Guid.CreateVersion7()}', '{Guid.CreateVersion7()}', 'PROCESSO_SELETIVO', '{Guid.CreateVersion7()}')
            """));

        erro.SqlState.Should().Be(ForeignKeyViolation);
    }

    [Fact(DisplayName = "Duas linhagens não ocupam a vaga do mesmo objeto: o índice único recusa a segunda")]
    public async Task Insert_SegundaVagaNoMesmoObjeto_Colide()
    {
        Guid processo = Guid.CreateVersion7();
        AtoNormativo primeiro = await GravarComVaga(processo);
        AtoNormativo segundo = Novo(unicoPorObjeto: true, vinculos: [("PROCESSO_SELETIVO", processo)]);

        // Grava o ato e o vínculo do segundo por via crua (o handler o recusaria antes),
        // para exercitar exatamente a trava do banco — e não a do handler.
        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
            await conexao.OpenAsync();
            await using NpgsqlTransaction tx = await conexao.BeginTransactionAsync();

            await ExecutarAsync(conexao, tx, InsertAtoCru(segundo));
            await ExecutarAsync(conexao, tx, InsertVinculoCru(segundo.Id, "PROCESSO_SELETIVO", processo));
            await ExecutarAsync(conexao, tx, InsertVagaCru(segundo.Id, segundo.Id, "PROCESSO_SELETIVO", processo));

            await tx.CommitAsync();
        });

        erro.SqlState.Should().Be(UniqueViolation);
        erro.ConstraintName.Should().Be("ux_linhagem_unica_por_objeto");
        primeiro.Id.Should().NotBe(segundo.Id);
    }

    [Fact(DisplayName = "Vaga forjada em nome de uma raiz que não é a da cadeia do ato é bloqueada")]
    public async Task Insert_VagaComRaizFalsa_Bloqueada()
    {
        Guid processo = Guid.CreateVersion7();
        AtoNormativo ato = Novo(unicoPorObjeto: true, vinculos: [("PROCESSO_SELETIVO", processo)]);
        AtoNormativo outro = Novo(unicoPorObjeto: true);

        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
            await conexao.OpenAsync();
            await using NpgsqlTransaction tx = await conexao.BeginTransactionAsync();

            await ExecutarAsync(conexao, tx, InsertAtoCru(ato));
            await ExecutarAsync(conexao, tx, InsertAtoCru(outro));
            await ExecutarAsync(conexao, tx, InsertVinculoCru(ato.Id, "PROCESSO_SELETIVO", processo));
            // A raiz declarada é outro ato, que nada tem com a cadeia deste.
            await ExecutarAsync(conexao, tx, InsertVagaCru(ato.Id, outro.Id, "PROCESSO_SELETIVO", processo));

            await tx.CommitAsync();
        });

        erro.SqlState.Should().Be(RestrictViolation);
    }

    [Fact(DisplayName = "Vaga de objeto a que o ato não se vincula é bloqueada")]
    public async Task Insert_VagaSemVinculo_Bloqueada()
    {
        Guid processo = Guid.CreateVersion7();
        AtoNormativo ato = Novo(unicoPorObjeto: true);

        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
            await conexao.OpenAsync();
            await using NpgsqlTransaction tx = await conexao.BeginTransactionAsync();

            await ExecutarAsync(conexao, tx, InsertAtoCru(ato));
            await ExecutarAsync(conexao, tx, InsertVagaCru(ato.Id, ato.Id, "PROCESSO_SELETIVO", processo));

            await tx.CommitAsync();
        });

        erro.SqlState.Should().Be(RestrictViolation);
    }

    [Fact(DisplayName = "Vínculo de ato único por objeto sem a vaga reservada é bloqueado — a correspondência é obrigatória")]
    public async Task Insert_VinculoSemVaga_Bloqueado()
    {
        // É esta trava que faz o índice único valer alguma coisa: sem ela, gravar o
        // vínculo e omitir a vaga deixaria o objeto livre para uma segunda linhagem, e o
        // índice, que só vê as vagas que existem, nada acusaria.
        Guid processo = Guid.CreateVersion7();
        AtoNormativo ato = Novo(unicoPorObjeto: true);

        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
            await conexao.OpenAsync();
            await using NpgsqlTransaction tx = await conexao.BeginTransactionAsync();

            await ExecutarAsync(conexao, tx, InsertAtoCru(ato));
            await ExecutarAsync(conexao, tx, InsertVinculoCru(ato.Id, "PROCESSO_SELETIVO", processo));

            await tx.CommitAsync();
        });

        erro.SqlState.Should().Be(RestrictViolation);
    }

    [Fact(DisplayName = "Ciclo de retificação inserido num único comando é bloqueado")]
    public async Task Insert_CicloDeRetificacao_Bloqueado()
    {
        // A chave estrangeira não barra isto: ela é verificada ao fim do COMANDO, e no fim
        // do comando os dois atos existem. O CHECK só cobre o ciclo de tamanho um, e o
        // índice único da linearidade não vê ciclo nenhum (A→B e B→A são alvos distintos).
        // Um ciclo gravado seria irreparável — UPDATE e DELETE estão bloqueados — e faria
        // toda travessia da cadeia girar para sempre.
        Guid a = Guid.CreateVersion7();
        Guid b = Guid.CreateVersion7();

        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(() => ExecutarCruAsync(
            $"""
            INSERT INTO publicacoes.ato_normativo
                (id, orgao, serie, ano, numero, tipo_codigo, congela_configuracao, efeito_irreversivel,
                 unico_por_objeto, data_publicacao, documento_hash, assinante, registrado_em,
                 ato_retificado_id, motivo_retificacao)
            VALUES
                ('{a}', 'CEPS', 'EDITAL', 2026, NULL, '{TipoUnico}', true, false, false,
                 DATE '2026-03-13', '{HashValido}', 'Jairo Belchior', now(), '{b}', 'ciclo'),
                ('{b}', 'CEPS', 'EDITAL', 2026, NULL, '{TipoUnico}', true, false, false,
                 DATE '2026-03-13', '{HashValido}', 'Jairo Belchior', now(), '{a}', 'ciclo')
            """));

        erro.SqlState.Should().Be(RestrictViolation);
    }

    [Fact(DisplayName = "TRUNCATE do vínculo é bloqueado — não é DELETE, e esvaziaria o registro sem disparar o append-only")]
    public async Task Truncate_DoVinculo_Bloqueado()
    {
        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(() => ExecutarCruAsync(
            "TRUNCATE publicacoes.vinculo_ato_entidade"));

        erro.SqlState.Should().Be(RestrictViolation);
    }

    [Fact(DisplayName = "TRUNCATE do ato publicado também é bloqueado")]
    public async Task Truncate_DoAto_Bloqueado()
    {
        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(() => ExecutarCruAsync(
            "TRUNCATE publicacoes.ato_normativo CASCADE"));

        erro.SqlState.Should().Be(RestrictViolation);
    }

    [Fact(DisplayName = "Vínculo de ato não único por objeto dispensa vaga")]
    public async Task Insert_VinculoDeAtoNaoUnico_NaoExigeVaga()
    {
        AtoNormativo ato = Novo(unicoPorObjeto: false, vinculos: [("CHAMADA", Guid.CreateVersion7())]);

        await Gravar(ato);

        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(userId: null);
        (await ctx.VinculosAtoEntidade.CountAsync(v => v.AtoId == ato.Id)).Should().Be(1);
        (await ctx.LinhagensUnicasPorObjeto.AnyAsync(l => l.AtoId == ato.Id)).Should().BeFalse();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<AtoNormativo> GravarComVaga(Guid processo)
    {
        AtoNormativo ato = Novo(unicoPorObjeto: true, vinculos: [("PROCESSO_SELETIVO", processo)]);

        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(userId: null);
        ctx.AtosNormativos.Add(ato);
        ctx.LinhagensUnicasPorObjeto.Add(
            LinhagemUnicaPorObjeto.Criar(ato, ato.Vinculos.Single(), raizId: ato.Id));
        await ctx.SaveChangesAsync();

        return ato;
    }

    private async Task Gravar(AtoNormativo ato)
    {
        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(userId: null);
        ctx.AtosNormativos.Add(ato);
        await ctx.SaveChangesAsync();
    }

    private async Task ExecutarCruAsync(string sql)
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();
        await using NpgsqlCommand comando = conexao.CreateCommand();
        comando.CommandText = sql;
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task ExecutarAsync(NpgsqlConnection conexao, NpgsqlTransaction transacao, string sql)
    {
        await using NpgsqlCommand comando = conexao.CreateCommand();
        comando.Transaction = transacao;
        comando.CommandText = sql;
        await comando.ExecuteNonQueryAsync();
    }

    private static string InsertAtoCru(AtoNormativo ato) =>
        $"""
        INSERT INTO publicacoes.ato_normativo
            (id, orgao, serie, ano, numero, tipo_codigo, congela_configuracao, efeito_irreversivel,
             unico_por_objeto, data_publicacao, documento_hash, assinante, registrado_em)
        VALUES
            ('{ato.Id}', 'CEPS', 'EDITAL', 2026, NULL, '{ato.TipoCodigo}', true, false,
             {(ato.UnicoPorObjeto ? "true" : "false")}, DATE '2026-03-13', '{HashValido}',
             'Jairo Belchior', now())
        """;

    private static string InsertVinculoCru(Guid atoId, string entidadeTipo, Guid entidadeId) =>
        $"""
        INSERT INTO publicacoes.vinculo_ato_entidade (id, ato_id, entidade_tipo, entidade_id)
        VALUES ('{Guid.CreateVersion7()}', '{atoId}', '{entidadeTipo}', '{entidadeId}')
        """;

    private static string InsertVagaCru(Guid atoId, Guid raizId, string entidadeTipo, Guid entidadeId) =>
        $"""
        INSERT INTO publicacoes.linhagem_unica_por_objeto
            (id, entidade_tipo, entidade_id, tipo_codigo, raiz_id, ato_id)
        VALUES ('{Guid.CreateVersion7()}', '{entidadeTipo}', '{entidadeId}', '{TipoUnico}', '{raizId}', '{atoId}')
        """;

    private static AtoNormativo Novo(
        bool unicoPorObjeto,
        IEnumerable<(string EntidadeTipo, Guid EntidadeId)>? vinculos = null) =>
        AtoNormativo.Registrar(
            Guid.CreateVersion7(),
            orgao: "CEPS",
            serie: "EDITAL",
            ano: 2026,
            numero: null,
            tipoCodigo: TipoUnico,
            congelaConfiguracao: true,
            efeitoIrreversivel: false,
            unicoPorObjeto: unicoPorObjeto,
            dataPublicacao: Publicacao,
            documentoHash: HashValido,
            assinante: "Jairo Belchior",
            registradoEm: Registro,
            versaoInvocada: null,
            vinculos: vinculos);
}
