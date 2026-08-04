namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Prova que a migration que introduz <see cref="ConfiguracaoClassificacao.BaseadoEmEnem"/>
/// (Story #850) preserva o comportamento anterior para configurações já persistidas.
/// </summary>
/// <remarks>
/// Antes desta migration, a aceitação de <c>ELIM-CORTE-REDACAO</c>/<c>ELIM-ZERO-EM-AREA</c>
/// dependia de <c>ProcessoSeletivo.Tipo</c> (SiSU/PSVR), não de um campo da própria
/// configuração — uma linha gravada nesse regime, com essa regra e um processo SiSU, é um
/// estado que <see cref="ConfiguracaoClassificacao.Criar"/> nunca mais aceitaria construir
/// depois da migration. O backfill promove exatamente esse sinal para a linha existente, sem
/// o que a canonicalização e a restauração do envelope divergiriam para configurações legadas
/// com o mesmo Id.
/// </remarks>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL fixo, escrito no próprio teste — o seed legado não recebe entrada externa.")]
[SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Recursos liberados por IAsyncLifetime.DisposeAsync — xUnit invoca deterministicamente.")]
public sealed class BackfillBaseadoEmEnemTests : IAsyncLifetime
{
    private const string MigrationAnterior = "20260804012509_AddUnidadeAdministradoraProcessoSeletivo";

    private static readonly Guid ProcessoSiSUId = new("77777777-7777-7777-8777-777777777771");
    private static readonly Guid ConfiguracaoSiSUId = new("77777777-7777-7777-8777-777777777772");
    private static readonly Guid RegraEliminacaoSiSUId = new("77777777-7777-7777-8777-777777777773");

    private static readonly Guid ProcessoPSVRId = new("99999999-9999-9999-8999-999999999991");
    private static readonly Guid ConfiguracaoPSVRId = new("99999999-9999-9999-8999-999999999992");
    private static readonly Guid RegraEliminacaoPSVRId = new("99999999-9999-9999-8999-999999999993");

    private static readonly Guid ProcessoNaoEnemId = new("88888888-8888-8888-8888-888888888881");
    private static readonly Guid ConfiguracaoNaoEnemId = new("88888888-8888-8888-8888-888888888882");

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_backfill_baseado_em_enem_tests")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync().ConfigureAwait(false);

    public async Task DisposeAsync() => await _postgres.DisposeAsync().ConfigureAwait(false);

    [Fact(DisplayName = "A migration promove BaseadoEmEnem=true para configuração legada de processo SiSU ou PSVR, e mantém false para processo não-ENEM")]
    public async Task Migration_PromoveBaseadoEmEnemConformeOTipoDoProcessoLegado()
    {
        await using (SelecaoDbContext contextoLegado = CriarContexto())
        {
            // Estado do mundo ANTES desta story: processos SiSU e PSVR com
            // ELIM-CORTE-REDACAO/ELIM-ZERO-EM-AREA configuradas — válido no regime
            // antigo, em que a aceitação vinha do Tipo do processo (SiSU=1 OU PSVR=4),
            // não de um campo próprio do bloco. Os dois tipos precisam de caso próprio:
            // um backfill que promovesse só um dos dois passaria despercebido se o
            // teste cobrisse apenas SiSU.
            IMigrator migrator = contextoLegado.GetService<IMigrator>();
            await migrator.MigrateAsync(MigrationAnterior);
            await SemearProcessoComClassificacaoAsync(
                ProcessoSiSUId, ConfiguracaoSiSUId, tipoProcesso: 1, regraEliminacaoId: RegraEliminacaoSiSUId);
            await SemearProcessoComClassificacaoAsync(
                ProcessoPSVRId, ConfiguracaoPSVRId, tipoProcesso: 4, regraEliminacaoId: RegraEliminacaoPSVRId);
            await SemearProcessoComClassificacaoAsync(
                ProcessoNaoEnemId, ConfiguracaoNaoEnemId, tipoProcesso: 2, regraEliminacaoId: null);
        }

        await using (SelecaoDbContext contextoNovo = CriarContexto())
        {
            await contextoNovo.Database.MigrateAsync();
        }

        await using SelecaoDbContext leitura = CriarContexto();

        ConfiguracaoClassificacao configuracaoSiSU = await leitura.Set<ConfiguracaoClassificacao>()
            .AsNoTracking()
            .SingleAsync(c => c.Id == ConfiguracaoSiSUId, CancellationToken.None);
        configuracaoSiSU.BaseadoEmEnem.Should().BeTrue(
            "o processo é SiSU (Tipo=1) — o backfill preserva o sinal que antes vinha do rótulo do processo, " +
            "sem o que a configuração legada (que já tem ELIM-CORTE-REDACAO persistida) ficaria num estado " +
            "que ConfiguracaoClassificacao.Criar nunca mais aceitaria construir");

        ConfiguracaoClassificacao configuracaoPSVR = await leitura.Set<ConfiguracaoClassificacao>()
            .AsNoTracking()
            .SingleAsync(c => c.Id == ConfiguracaoPSVRId, CancellationToken.None);
        configuracaoPSVR.BaseadoEmEnem.Should().BeTrue(
            "o processo é PSVR (Tipo=4) — o segundo membro do predicado do backfill, provado à parte para não " +
            "deixar passar uma migration que só promovesse SiSU");

        ConfiguracaoClassificacao configuracaoNaoEnem = await leitura.Set<ConfiguracaoClassificacao>()
            .AsNoTracking()
            .SingleAsync(c => c.Id == ConfiguracaoNaoEnemId, CancellationToken.None);
        configuracaoNaoEnem.BaseadoEmEnem.Should().BeFalse(
            "o processo é PSIQ (Tipo=2) — o backfill não promove configurações de processos fora de SiSU/PSVR");
    }

    private SelecaoDbContext CriarContexto()
    {
        DbContextOptions<SelecaoDbContext> options = new DbContextOptionsBuilder<SelecaoDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;

        return new SelecaoDbContext(options);
    }

    /// <summary>
    /// Semeia, em SQL cru, um processo e a classificação dele como o modelo ANTIGO
    /// gravava — sem a coluna <c>baseado_em_enem</c>, que só a migration desta story cria.
    /// </summary>
    private async Task SemearProcessoComClassificacaoAsync(
        Guid processoId, Guid configuracaoId, int tipoProcesso, Guid? regraEliminacaoId)
    {
        await using NpgsqlConnection conexao = new(_postgres.GetConnectionString());
        await conexao.OpenAsync();

        await ExecutarAsync(conexao, $$"""
            INSERT INTO selecao.processos_seletivos (id, nome, tipo, status, created_at, is_deleted)
            VALUES ('{{processoId}}', 'PS legado', {{tipoProcesso}}, 1, now(), false);

            INSERT INTO selecao.configuracoes_classificacao (
                id, processo_seletivo_id,
                regra_calculo_codigo, regra_calculo_versao, regra_calculo_hash,
                regra_arredondamento_codigo, regra_arredondamento_versao, regra_arredondamento_hash,
                casas_arredondamento,
                regra_ordem_alocacao_codigo, regra_ordem_alocacao_versao, regra_ordem_alocacao_hash,
                n_opcoes_alocacao, created_at)
            VALUES (
                '{{configuracaoId}}', '{{processoId}}',
                'FORMULA-MEDIA-PONDERADA', '1', repeat('a', 64),
                'ARREDONDAMENTO-TRUNCAR', '1', repeat('b', 64),
                2,
                'ALOCACAO-OPCOES-RN04', '1', repeat('c', 64),
                1, now());
            """);

        if (regraEliminacaoId is { } id)
        {
            await ExecutarAsync(conexao, $$"""
                INSERT INTO selecao.regras_eliminacao (
                    id, configuracao_classificacao_id, regra_codigo, regra_versao, regra_hash, args, created_at)
                VALUES (
                    '{{id}}', '{{configuracaoId}}', 'ELIM-CORTE-REDACAO', '1', repeat('d', 64),
                    '{"$tipo":"corteRedacao","minimo":400}', now());
                """);
        }
    }

    private static async Task ExecutarAsync(NpgsqlConnection conexao, string sql)
    {
        await using NpgsqlCommand comando = new(sql, conexao);
        await comando.ExecuteNonQueryAsync();
    }
}
