namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Prova, contra Postgres real, as garantias que a <see cref="VersaoConfiguracao"/>
/// delega ao BANCO — e não à disciplina de quem escreve o handler (ADR-0104,
/// ADR-0063): append-only, numeração contígua, cadeia única por certame, um ato
/// criando no máximo uma versão, e vigência sem unicidade.
/// </summary>
/// <remarks>
/// Todo INSERT aqui é cru, via <see cref="NpgsqlCommand"/>, deliberadamente
/// fora do agregado: forja exatamente os estados que as factories de domínio
/// nunca produziriam. Um teste que só exercitasse o caminho feliz do EF Core
/// provaria o domínio de novo — não o banco.
/// </remarks>
public sealed class VersaoConfiguracaoPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private const string UniqueViolation = "23505";
    private const string CheckViolation = "23514";
    private const string RestrictViolation = "23001";

    private readonly ProcessoSeletivoDbFixture _fixture;

    public VersaoConfiguracaoPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "UPDATE cru é bloqueado pelo trigger append-only — a configuração congelada não se muta")]
    public async Task Update_Cru_Bloqueado()
    {
        Publicacao publicada = await PublicarAsync(nameof(Update_Cru_Bloqueado));

        Func<Task> update = () => ExecutarAsync(
            "UPDATE selecao.versoes_configuracao SET ator_usuario_conta_id = 'outro' WHERE id = @id",
            ("id", publicada.VersaoId));

        (await update.Should().ThrowAsync<PostgresException>())
            .Which.MessageText.Should().Contain("append-only");
    }

    [Fact(DisplayName = "DELETE cru é bloqueado pelo trigger append-only — e a linha continua lá")]
    public async Task Delete_Cru_Bloqueado()
    {
        Publicacao publicada = await PublicarAsync(nameof(Delete_Cru_Bloqueado));

        Func<Task> delete = () => ExecutarAsync(
            "DELETE FROM selecao.versoes_configuracao WHERE id = @id",
            ("id", publicada.VersaoId));

        await delete.Should().ThrowAsync<PostgresException>();

        // A prova de que o bloqueio é do banco, não do EF: a linha sobreviveu.
        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        bool aindaExiste = await readContext.VersoesConfiguracao
            .AsNoTracking()
            .AnyAsync(v => v.Id == publicada.VersaoId, CancellationToken.None);
        aindaExiste.Should().BeTrue();
    }

    [Fact(DisplayName = "TRUNCATE é bloqueado — não dispara trigger de linha, e esvaziaria o registro pelas costas do append-only")]
    public async Task Truncate_Bloqueado()
    {
        await PublicarAsync(nameof(Truncate_Bloqueado));

        Func<Task> truncate = () => ExecutarAsync("TRUNCATE selecao.versoes_configuracao CASCADE");

        (await truncate.Should().ThrowAsync<PostgresException>())
            .Which.SqlState.Should().Be(RestrictViolation);
    }

    [Fact(DisplayName = "Os três triggers da tabela existem no catálogo do banco")]
    public async Task Triggers_ExistemNoCatalogo()
    {
        IReadOnlyList<string> triggers = await ListarTriggersAsync();

        triggers.Should().BeEquivalentTo(
        [
            "trg_versoes_configuracao_bloqueia_truncate",
            "trg_versoes_configuracao_somente_insercao",
            "trg_versoes_configuracao_sucessao",
        ]);
    }

    // O ato que abriu o certame acima tentando também abrir a configuração de
    // um segundo certame. É por isso que o índice é único GLOBALMENTE, e não
    // por processo: dentro da mesma cadeia, um ato repetido já seria barrado
    // antes (ele teria de retificar a si mesmo, o que
    // ck_versoes_configuracao_nao_autorretifica recusa).
    [Fact(DisplayName = "Um ato cria no máximo uma versão — nem sequer em OUTRO certame: o índice único do ato criador é global")]
    public async Task Insert_MesmoAtoCriador_Colide()
    {
        Publicacao publicada = await PublicarAsync(nameof(Insert_MesmoAtoCriador_Colide));
        Guid outroProcesso = await SemearProcessoEmRascunhoAsync(nameof(Insert_MesmoAtoCriador_Colide) + "-outro");

        PostgresException erro = await CapturarErroAsync(InsercaoDeVersaoConfiguracao.Criar(
            outroProcesso,
            numeroVersao: 1,
            atoCriadorId: publicada.AtoCriadorId,
            atoCriadorRetificaId: null));

        erro.SqlState.Should().Be(UniqueViolation);
        erro.ConstraintName.Should().Be("ux_versoes_configuracao_ato_criador");
    }

    [Fact(DisplayName = "Um ato não retifica a si mesmo — a cadeia de atos congelantes é linear")]
    public async Task Insert_AtoRetificandoASiMesmo_Recusado()
    {
        Publicacao publicada = await PublicarAsync(nameof(Insert_AtoRetificandoASiMesmo_Recusado));

        PostgresException erro = await CapturarErroAsync(InsercaoDeVersaoConfiguracao.Criar(
            publicada.ProcessoId,
            numeroVersao: 2,
            atoCriadorId: publicada.AtoCriadorId,
            atoCriadorRetificaId: publicada.AtoCriadorId));

        erro.SqlState.Should().Be(CheckViolation);
        erro.ConstraintName.Should().Be("ck_versoes_configuracao_nao_autorretifica");
    }

    [Fact(DisplayName = "Numeração com buraco é recusada: a versão 3 sem a 2 não entra")]
    public async Task Insert_NumeracaoComBuraco_Recusada()
    {
        Publicacao publicada = await PublicarAsync(nameof(Insert_NumeracaoComBuraco_Recusada));

        PostgresException erro = await CapturarErroAsync(InsercaoDeVersaoConfiguracao.Criar(
            publicada.ProcessoId,
            numeroVersao: 3,
            atoCriadorId: Guid.CreateVersion7(),
            atoCriadorRetificaId: publicada.AtoCriadorId));

        erro.SqlState.Should().Be(CheckViolation);
        erro.ConstraintName.Should().Be("ck_versoes_configuracao_numeracao_contigua");
    }

    [Fact(DisplayName = "A primeira versão de um processo tem de ser a 1 — não se abre a cadeia pelo meio")]
    public async Task Insert_PrimeiraVersaoNaoEUm_Recusada()
    {
        Guid processoId = await SemearProcessoEmRascunhoAsync(nameof(Insert_PrimeiraVersaoNaoEUm_Recusada));

        PostgresException erro = await CapturarErroAsync(InsercaoDeVersaoConfiguracao.Criar(
            processoId,
            numeroVersao: 2,
            atoCriadorId: Guid.CreateVersion7(),
            atoCriadorRetificaId: Guid.CreateVersion7()));

        erro.SqlState.Should().Be(CheckViolation);
        erro.ConstraintName.Should().Be("ck_versoes_configuracao_numeracao_contigua");
    }

    [Fact(DisplayName = "Cadeia quebrada é recusada: a versão 2 cujo ato criador não retifica o criador da versão 1")]
    public async Task Insert_CadeiaQuebrada_Recusada()
    {
        Publicacao publicada = await PublicarAsync(nameof(Insert_CadeiaQuebrada_Recusada));

        // O ato criador retifica um ato QUALQUER — não o criador da versão 1.
        // É a tentativa de abrir uma segunda cadeia de configuração no certame.
        PostgresException erro = await CapturarErroAsync(InsercaoDeVersaoConfiguracao.Criar(
            publicada.ProcessoId,
            numeroVersao: 2,
            atoCriadorId: Guid.CreateVersion7(),
            atoCriadorRetificaId: Guid.CreateVersion7()));

        erro.SqlState.Should().Be(CheckViolation);
        erro.ConstraintName.Should().Be("ck_versoes_configuracao_cadeia");
    }

    [Fact(DisplayName = "Número de versão duplicado colide no índice único (processo, número) — é a trava da corrida de publicações concorrentes")]
    public async Task Insert_NumeroDuplicado_Colide()
    {
        Publicacao publicada = await PublicarAsync(nameof(Insert_NumeroDuplicado_Colide));

        // Duas publicações concorrentes derivam o mesmo N+1 do mesmo topo; o
        // trigger de sucessão deixa passar (ambas leem o mesmo estado) e quem
        // recusa a segunda é o índice único — com erro de DUPLICIDADE, não de
        // buraco na numeração.
        PostgresException erro = await CapturarErroAsync(InsercaoDeVersaoConfiguracao.Criar(
            publicada.ProcessoId,
            numeroVersao: 1,
            atoCriadorId: Guid.CreateVersion7(),
            atoCriadorRetificaId: null));

        erro.SqlState.Should().Be(UniqueViolation);
        erro.ConstraintName.Should().Be("ux_versoes_configuracao_processo_numero");
    }

    [Fact(DisplayName = "Contrato simétrico: a versão 1 não pode retificar ato algum")]
    public async Task Insert_VersaoUmRetificando_Recusada()
    {
        Guid processoId = await SemearProcessoEmRascunhoAsync(nameof(Insert_VersaoUmRetificando_Recusada));

        PostgresException erro = await CapturarErroAsync(InsercaoDeVersaoConfiguracao.Criar(
            processoId,
            numeroVersao: 1,
            atoCriadorId: Guid.CreateVersion7(),
            atoCriadorRetificaId: Guid.CreateVersion7()));

        erro.SqlState.Should().Be(CheckViolation);
        erro.ConstraintName.Should().Be("ck_versoes_configuracao_contrato_abertura");
    }

    // Dois guard rails cobrem esta linha — ck_versoes_configuracao_contrato_abertura
    // (sucessora exige ato retificado) e o trigger de sucessão (o ato retificado
    // tem de ser o criador da versão anterior). Quem fala é o trigger: no
    // Postgres, o BEFORE INSERT roda ANTES da avaliação dos CHECK constraints,
    // e um retifica_id nulo já é "distinto" do criador da versão 1. O
    // diagnóstico é o mais informativo dos dois, e é o que o handler traduz.
    [Fact(DisplayName = "Contrato simétrico: uma versão N > 1 sem ato retificado é recusada — sucessora órfã não entra")]
    public async Task Insert_SucessoraSemRetificacao_Recusada()
    {
        Publicacao publicada = await PublicarAsync(nameof(Insert_SucessoraSemRetificacao_Recusada));

        PostgresException erro = await CapturarErroAsync(InsercaoDeVersaoConfiguracao.Criar(
            publicada.ProcessoId,
            numeroVersao: 2,
            atoCriadorId: Guid.CreateVersion7(),
            atoCriadorRetificaId: null));

        erro.SqlState.Should().Be(CheckViolation);
        erro.ConstraintName.Should().Be("ck_versoes_configuracao_cadeia");
    }

    // É o ponto da ADR-0104: a data documental deixa de ordenar, e o
    // instante da vigência não precisa ser único — quem desempata é o
    // número da versão.
    [Fact(DisplayName = "Duas versões no MESMO instante de vigência são aceitas — o empate de instante deixa de ser impossível por construção")]
    public async Task Insert_MesmoInstanteDeVigencia_Aceito()
    {
        Publicacao publicada = await PublicarAsync(nameof(Insert_MesmoInstanteDeVigencia_Aceito));

        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        DateTimeOffset instanteDaAbertura = await ObterInstanteDeVigenciaAsync(readContext, publicada.VersaoId);

        await ExecutarAsync(CriarSucessoraComVigencia(publicada, instanteDaAbertura));

        int versoesNoMesmoInstante = await readContext.VersoesConfiguracao
            .AsNoTracking()
            .CountAsync(
                v => v.ProcessoSeletivoId == publicada.ProcessoId && v.VigenteAPartirDe == instanteDaAbertura,
                CancellationToken.None);
        versoesNoMesmoInstante.Should().Be(2, "duas versões podem partilhar o instante de vigência sem colidir");
    }

    // É a vigência que ordena as versões: uma sucessora nascida no passado
    // faria o seletor continuar elegendo a versão VELHA depois de a nova
    // existir. O domínio empata (nunca regride) quando o relógio do host
    // recua; o banco recusa o retrocesso vindo por fora.
    [Fact(DisplayName = "Vigência não regride: uma sucessora com vigência anterior à da versão que sucede é recusada")]
    public async Task Insert_VigenciaRegressiva_Recusada()
    {
        Publicacao publicada = await PublicarAsync(nameof(Insert_VigenciaRegressiva_Recusada));

        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        DateTimeOffset instanteDaAbertura = await ObterInstanteDeVigenciaAsync(readContext, publicada.VersaoId);

        PostgresException erro = await CapturarErroAsync(CriarSucessoraComVigencia(publicada, instanteDaAbertura.AddMinutes(-10)));

        erro.SqlState.Should().Be(CheckViolation);
        erro.ConstraintName.Should().Be("ck_versoes_configuracao_vigencia_monotonica");
    }

    [Fact(DisplayName = "O índice de vigência existe e NÃO é único — a unicidade de instante seria justamente o defeito corrigido")]
    public async Task IndiceDeVigencia_ExisteENaoEUnico()
    {
        IReadOnlyList<string> unicidade = await ListarAsync("""
            SELECT i.indisunique::text
            FROM pg_index i
            JOIN pg_class c ON c.oid = i.indexrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'selecao'
              AND c.relname = 'ix_versoes_configuracao_processo_vigencia'
            """);

        unicidade.Should().ContainSingle().Which.Should().Be("false");
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------
    private sealed record Publicacao(Guid ProcessoId, Guid AtoCriadorId, Guid VersaoId);

    private async Task<Publicacao> PublicarAsync(string nome)
    {
        ProcessoSeletivoPublicacaoSeeder.Resultado semeado =
            await ProcessoSeletivoPublicacaoSeeder.PublicarAsync(_fixture, nome);

        return new Publicacao(semeado.ProcessoId, semeado.AtoId, semeado.VersaoId);
    }

    private async Task<Guid> SemearProcessoEmRascunhoAsync(string nome)
    {
        ProcessoSeletivo processo = ProcessoSeletivoPublicacaoSeeder.NovoProcessoConforme(nome);

        await using UniPlusDbContext context = _fixture.CreateDbContext();
        await context.ProcessosSeletivos.AddAsync(processo, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        return processo.Id;
    }

    private async Task<IReadOnlyList<string>> ListarTriggersAsync() =>
        await ListarAsync("""
            SELECT tgname::text
            FROM pg_trigger t
            JOIN pg_class c ON c.oid = t.tgrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'selecao'
              AND c.relname = 'versoes_configuracao'
              AND NOT t.tgisinternal
            ORDER BY tgname
            """);

    private static string CriarSucessoraComVigencia(Publicacao publicada, DateTimeOffset vigencia) =>
        InsercaoDeVersaoConfiguracao.Criar(
            publicada.ProcessoId, 2, Guid.CreateVersion7(), publicada.AtoCriadorId, vigencia);

    private static async Task<DateTimeOffset> ObterInstanteDeVigenciaAsync(UniPlusDbContext context, Guid versaoId) =>
        await context.VersoesConfiguracao
            .AsNoTracking()
            .Where(v => v.Id == versaoId)
            .Select(v => v.VigenteAPartirDe)
            .FirstAsync(CancellationToken.None);

    private async Task<PostgresException> CapturarErroAsync(string sql) =>
        await Assert.ThrowsAsync<PostgresException>(() => ExecutarAsync(sql));

    private async Task ExecutarAsync(string sql, params (string Nome, object Valor)[] parametros)
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using NpgsqlCommand comando = new(sql, conexao);
        foreach ((string nome, object valor) in parametros)
        {
            comando.Parameters.AddWithValue(nome, valor);
        }

        await comando.ExecuteNonQueryAsync();
    }

    private async Task<IReadOnlyList<string>> ListarAsync(string sql)
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();

        await using NpgsqlCommand comando = new(sql, conexao);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();

        List<string> valores = [];
        while (await leitor.ReadAsync())
        {
            valores.Add(leitor.GetString(0));
        }

        return valores;
    }
}
