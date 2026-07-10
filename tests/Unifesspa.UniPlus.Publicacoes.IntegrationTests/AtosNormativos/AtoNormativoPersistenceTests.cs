namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.AtosNormativos;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

/// <summary>
/// Integração do ato normativo contra Postgres real: o append-only imposto por
/// trigger (UPDATE/DELETE crus bloqueados), os CHECKs de shape, a ausência de
/// unicidade do número, e o round-trip do par por valor {id, hash}.
/// </summary>
[Collection(PublicacoesDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class AtoNormativoPersistenceTests
{
    private const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string CheckViolation = "23514";
    private static readonly DateOnly Publicacao = new(2026, 3, 13);
    private static readonly DateTimeOffset Registro = new(2026, 3, 13, 19, 0, 0, TimeSpan.Zero);

    private readonly PublicacoesDbFixture _fixture;

    public AtoNormativoPersistenceTests(PublicacoesDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Persiste e relê os campos do ato, com a versão invocada por valor")]
    public async Task Insert_PersisteCampos_ComVersaoInvocada()
    {
        ReferenciaVersaoConfiguracao versao = ReferenciaVersaoConfiguracao
            .Criar(Guid.CreateVersion7(), HashValido).Value!;
        AtoNormativo ato = Novo(numero: "13", versao: versao);
        await Gravar(ato);

        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(userId: null);
        AtoNormativo persistido = await ctx.AtosNormativos.SingleAsync(a => a.Id == ato.Id);

        persistido.Orgao.Should().Be("CEPS");
        persistido.Numero.Should().Be("13");
        persistido.DocumentoHash.Should().Be(HashValido);
        persistido.RegistradoEm.Should().Be(Registro);
        persistido.VersaoInvocada.Should().NotBeNull();
        persistido.VersaoInvocada!.Id.Should().Be(versao.Id);
        persistido.VersaoInvocada.Hash.Should().Be(versao.Hash);
    }

    [Fact(DisplayName = "Ato sem versão invocada persiste com o par ausente (owned opcional)")]
    public async Task Insert_SemVersaoInvocada_ParAusente()
    {
        AtoNormativo ato = Novo(numero: null, versao: null);
        await Gravar(ato);

        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(userId: null);
        AtoNormativo persistido = await ctx.AtosNormativos.SingleAsync(a => a.Id == ato.Id);

        persistido.Numero.Should().BeNull();
        persistido.VersaoInvocada.Should().BeNull();
    }

    [Fact(DisplayName = "UPDATE cru é bloqueado pelo trigger append-only")]
    public async Task Update_Cru_Bloqueado()
    {
        AtoNormativo ato = Novo();
        await Gravar(ato);

        Func<Task> update = () => ExecutarSql(
            "UPDATE publicacoes.ato_normativo SET assinante = 'Outro' WHERE id = @id",
            ("id", ato.Id));

        (await update.Should().ThrowAsync<PostgresException>())
            .Which.MessageText.Should().Contain("append-only");
    }

    [Fact(DisplayName = "DELETE cru é bloqueado pelo trigger append-only")]
    public async Task Delete_Cru_Bloqueado()
    {
        AtoNormativo ato = Novo();
        await Gravar(ato);

        Func<Task> delete = () => ExecutarSql(
            "DELETE FROM publicacoes.ato_normativo WHERE id = @id",
            ("id", ato.Id));

        await delete.Should().ThrowAsync<PostgresException>();

        // A prova de que o bloqueio é do banco: a linha continua lá.
        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(userId: null);
        (await ctx.AtosNormativos.AnyAsync(a => a.Id == ato.Id)).Should().BeTrue();
    }

    [Fact(DisplayName = "O trigger append-only existe no catálogo do banco")]
    public async Task Trigger_ExisteNoCatalogo()
    {
        object? nome = await ExecutarScalar(
            """
            SELECT tgname::text
            FROM pg_trigger
            WHERE tgname = 'trg_ato_normativo_somente_insercao'
              AND NOT tgisinternal
            """);

        nome.Should().Be("trg_ato_normativo_somente_insercao");
    }

    [Fact(DisplayName = "Dois atos com a mesma numeração são aceitos — número não tem unicidade")]
    public async Task Insert_MesmaNumeracao_Aceito()
    {
        await Gravar(Novo(numero: "13"));
        Func<Task> segunda = () => Gravar(Novo(numero: "13"));

        await segunda.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "Três atos do mesmo certame no mesmo dia são aceitos (sem ordem total)")]
    public async Task Insert_TresNoMesmoDia_Aceito()
    {
        await Gravar(Novo(numero: "13"));
        await Gravar(Novo(numero: "14"));
        await Gravar(Novo(numero: "15"));

        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(userId: null);
        int doDia = await ctx.AtosNormativos.CountAsync(a => a.DataPublicacao == Publicacao);
        doDia.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact(DisplayName = "O índice de numeração existe e NÃO é único")]
    public async Task IndiceNumeracao_ExisteENaoEUnico()
    {
        object? indisunique = await ExecutarScalar(
            """
            SELECT i.indisunique
            FROM pg_class c
            JOIN pg_index i ON i.indexrelid = c.oid
            WHERE c.relname = 'ix_ato_normativo_numeracao'
            """);

        indisunique.Should().Be(false);
    }

    [Fact(DisplayName = "CHECK recusa hash de documento fora do formato (insert cru)")]
    public async Task CheckDocumentoHash_RecusaFormatoInvalido()
    {
        Func<Task> insercao = () => InserirCru(documentoHash: "nao-e-hash");
        (await insercao.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(CheckViolation);
    }

    [Fact(DisplayName = "CHECK recusa par de versão incompleto (só id, insert cru)")]
    public async Task CheckVersaoCompleta_RecusaParIncompleto()
    {
        Func<Task> insercao = () => InserirCru(versaoId: Guid.CreateVersion7(), versaoHash: null);
        (await insercao.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(CheckViolation);
    }

    [Fact(DisplayName = "CHECK recusa par de versão incompleto (só hash, insert cru)")]
    public async Task CheckVersaoCompleta_RecusaSoHash()
    {
        Func<Task> insercao = () => InserirCru(versaoId: null, versaoHash: HashValido);
        (await insercao.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(CheckViolation);
    }

    [Fact(DisplayName = "CHECK recusa hash de versão fora do formato (insert cru)")]
    public async Task CheckVersaoHash_RecusaFormatoInvalido()
    {
        Func<Task> insercao = () => InserirCru(versaoId: Guid.CreateVersion7(), versaoHash: new string('g', 64));
        (await insercao.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(CheckViolation);
    }

    [Fact(DisplayName = "CHECK recusa versão com id zerado (insert cru)")]
    public async Task CheckVersaoIdNaoZero_RecusaGuidEmpty()
    {
        Func<Task> insercao = () => InserirCru(versaoId: Guid.Empty, versaoHash: HashValido);
        (await insercao.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(CheckViolation);
    }

    [Fact(DisplayName = "CHECK recusa ano não-positivo (insert cru)")]
    public async Task CheckAno_RecusaNaoPositivo()
    {
        Func<Task> insercao = () => InserirCru(ano: 0);
        (await insercao.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(CheckViolation);
    }

    [Fact(DisplayName = "ListarIdsComMesmaNumeracao devolve os conflitantes, ignorando o próprio ato")]
    public async Task ListarIdsComMesmaNumeracao_IgnoraProprio()
    {
        AtoNormativo a1 = Novo(numero: "99");
        AtoNormativo a2 = Novo(numero: "99");
        await Gravar(a1);
        await Gravar(a2);

        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(userId: null);
        var repo = new AtoNormativoRepository(ctx);

        IReadOnlyList<Guid> conflitantesDeA1 = await repo.ListarIdsComMesmaNumeracaoAsync(
            "CEPS", "EDITAL", 2026, "99", a1.Id, default);

        conflitantesDeA1.Should().Contain(a2.Id).And.NotContain(a1.Id);
    }

    [Fact(DisplayName = "Listagem paginada por cursor devolve os atos em ordem de Id")]
    public async Task ListarPaginado_OrdenaPorId()
    {
        AtoNormativo a1 = Novo(numero: "201");
        AtoNormativo a2 = Novo(numero: "202");
        await Gravar(a1);
        await Gravar(a2);

        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(userId: null);
        var repo = new AtoNormativoRepository(ctx);

        (IReadOnlyList<AtoNormativo> itens, _, _) = await repo.ListarPaginadoAsync(
            afterId: null, limit: 100, PaginationDirection.Next, default);

        itens.Select(a => a.Id).Should().ContainInOrder(
            new[] { a1.Id, a2.Id }.OrderBy(id => id));
    }

    [Fact(DisplayName = "Paginação Prev sobre a entidade forense devolve a página anterior em ordem ascendente")]
    public async Task ListarPaginado_Prev_FuncionaComEntidadeForense()
    {
        // A entidade forense não é EntityBase; a paginação keyset (relaxada para
        // IIdentificavel) precisa ordená-la e fatiá-la corretamente também no sentido Prev.
        AtoNormativo a1 = Novo(numero: "301");
        AtoNormativo a2 = Novo(numero: "302");
        AtoNormativo a3 = Novo(numero: "303");
        await Gravar(a1);
        await Gravar(a2);
        await Gravar(a3);

        Guid[] ordenados = new[] { a1.Id, a2.Id, a3.Id }.OrderBy(id => id).ToArray();

        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(userId: null);
        var repo = new AtoNormativoRepository(ctx);

        // Ancorado no maior dos três, Prev traz os anteriores em ordem ascendente
        // (o probe é cortado ainda em DESC e a lista revertida). Limit alto e
        // asserções relativas porque o container é compartilhado entre os testes.
        (IReadOnlyList<AtoNormativo> anteriores, _, _) = await repo.ListarPaginadoAsync(
            afterId: ordenados[2], limit: 100, PaginationDirection.Prev, default);

        IReadOnlyList<Guid> ids = [.. anteriores.Select(a => a.Id)];
        ids.Should().NotContain(ordenados[2], "Prev exclui a âncora");
        ids.Should().ContainInOrder(ordenados[0], ordenados[1]);
        ids.Should().BeInAscendingOrder();
    }

    private async Task Gravar(AtoNormativo ato)
    {
        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(userId: null);
        ctx.AtosNormativos.Add(ato);
        await ctx.SaveChangesAsync();
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "SQL fixo dos testes de integração, sem entrada de usuário; parâmetros via NpgsqlParameter.")]
    private async Task ExecutarSql(string sql, params (string Nome, object Valor)[] parametros)
    {
        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(userId: null);
        await ctx.Database.OpenConnectionAsync();

        await using var command = new NpgsqlCommand(sql, (NpgsqlConnection)ctx.Database.GetDbConnection());
        foreach ((string nome, object valor) in parametros)
        {
            command.Parameters.AddWithValue(nome, valor);
        }

        await command.ExecuteNonQueryAsync();
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "SQL fixo dos testes de integração, sem entrada de usuário.")]
    private async Task<object?> ExecutarScalar(string sql)
    {
        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(userId: null);
        await ctx.Database.OpenConnectionAsync();

        await using var command = new NpgsqlCommand(sql, (NpgsqlConnection)ctx.Database.GetDbConnection());
        return await command.ExecuteScalarAsync();
    }

    private async Task InserirCru(
        int ano = 2026,
        string documentoHash = HashValido,
        Guid? versaoId = null,
        string? versaoHash = null)
    {
        await using PublicacoesDbContext ctx = _fixture.CreateDbContext(userId: null);
        await ctx.Database.OpenConnectionAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO publicacoes.ato_normativo
                (id, orgao, serie, ano, numero, tipo_codigo, congela_configuracao, efeito_irreversivel,
                 data_publicacao, documento_hash, assinante, registrado_em, versao_invocada_id, versao_invocada_hash)
            VALUES
                (gen_random_uuid(), 'CEPS', 'EDITAL', @ano, '13', 'EDITAL_ABERTURA', false, false,
                 @data, @hash, 'Assinante', now(), @versao_id, @versao_hash)
            """,
            (NpgsqlConnection)ctx.Database.GetDbConnection());

        command.Parameters.AddWithValue("ano", ano);
        command.Parameters.AddWithValue("data", Publicacao);
        command.Parameters.AddWithValue("hash", documentoHash);
        command.Parameters.AddWithValue("versao_id", versaoId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("versao_hash", versaoHash ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    private static AtoNormativo Novo(string? numero = "13", ReferenciaVersaoConfiguracao? versao = null) =>
        AtoNormativo.Registrar(
            orgao: "CEPS",
            serie: "EDITAL",
            ano: 2026,
            numero: numero,
            tipoCodigo: "EDITAL_ABERTURA",
            congelaConfiguracao: false,
            efeitoIrreversivel: false,
            dataPublicacao: Publicacao,
            documentoHash: HashValido,
            assinante: "Jairo Belchior",
            registradoEm: Registro,
            versaoInvocada: versao);
}
