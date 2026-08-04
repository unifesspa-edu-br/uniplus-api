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
/// Prova que a migration que introduz <see cref="FatoColetado.Rotulo"/>/
/// <see cref="FatoColetado.TipoRenderizacao"/> (Story #559) preserva um estado coerente para
/// fatos coletados já persistidos.
/// </summary>
/// <remarks>
/// Antes desta migration, <c>fatos_coletados</c> não tinha apresentação — uma linha gravada
/// nesse regime é um estado que <see cref="FatoColetado.Criar"/> nunca mais aceitaria construir
/// depois da migration (rótulo vazio e <c>TipoRenderizacao.Nenhuma</c> são ambos recusados). O
/// backfill promove, para os dois códigos do vocabulário fechado observados em dado real
/// (<c>COR_RACA</c>, <c>BAIXA_RENDA</c>), o rótulo do catálogo e o tipo de renderização coerente
/// com o domínio/cardinalidade do fato. Um código fora dessa lista permanece no estado
/// pré-migration — limite documentado do backfill, não uma omissão silenciosa.
/// </remarks>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL fixo, escrito no próprio teste — o seed legado não recebe entrada externa.")]
[SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Recursos liberados por IAsyncLifetime.DisposeAsync — xUnit invoca deterministicamente.")]
public sealed class BackfillFormularioInscricaoTests : IAsyncLifetime
{
    private const string MigrationAnterior = "20260804035257_DefineBaseadoEmEnem";

    private static readonly Guid ProcessoId = new("66666666-6666-6666-8666-666666666661");
    private static readonly Guid CorRacaId = new("66666666-6666-6666-8666-666666666662");
    private static readonly Guid BaixaRendaId = new("66666666-6666-6666-8666-666666666663");
    private static readonly Guid OutroFatoId = new("66666666-6666-6666-8666-666666666664");

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_backfill_formulario_inscricao_tests")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync().ConfigureAwait(false);

    public async Task DisposeAsync() => await _postgres.DisposeAsync().ConfigureAwait(false);

    [Fact(DisplayName = "A migration promove Rotulo/TipoRenderizacao coerentes para COR_RACA e BAIXA_RENDA legados, e não toca em código fora do vocabulário conhecido")]
    public async Task Migration_PromoveApresentacaoConformeOVocabularioFechado()
    {
        await using (SelecaoDbContext contextoLegado = CriarContexto())
        {
            IMigrator migrator = contextoLegado.GetService<IMigrator>();
            await migrator.MigrateAsync(MigrationAnterior);
            await SemearFatoColetadoLegadoAsync();
        }

        await using (SelecaoDbContext contextoNovo = CriarContexto())
        {
            await contextoNovo.Database.MigrateAsync();
        }

        await using SelecaoDbContext leitura = CriarContexto();

        FatoColetado corRaca = await leitura.Set<FatoColetado>().AsNoTracking().SingleAsync(f => f.Id == CorRacaId, CancellationToken.None);
        corRaca.Rotulo.Should().Be("Cor ou raça", "o backfill promove o rótulo do catálogo para o código conhecido COR_RACA");
        corRaca.TipoRenderizacao.Should().Be(
            Unifesspa.UniPlus.Selecao.Domain.Enums.TipoRenderizacao.SelecaoUnica,
            "COR_RACA é CATEGORICO/ESCALAR no catálogo — o tipo coerente é seleção única");

        FatoColetado baixaRenda = await leitura.Set<FatoColetado>().AsNoTracking().SingleAsync(f => f.Id == BaixaRendaId, CancellationToken.None);
        baixaRenda.Rotulo.Should().Be(
            "Renda familiar per capita igual ou inferior a um salário mínimo",
            "o backfill promove o rótulo do catálogo para o código conhecido BAIXA_RENDA");
        baixaRenda.TipoRenderizacao.Should().Be(
            Unifesspa.UniPlus.Selecao.Domain.Enums.TipoRenderizacao.Booleano,
            "BAIXA_RENDA é BOOLEANO no catálogo — o tipo coerente é booleano");

        FatoColetado outroFato = await leitura.Set<FatoColetado>().AsNoTracking().SingleAsync(f => f.Id == OutroFatoId, CancellationToken.None);
        outroFato.Rotulo.Should().BeEmpty(
            "um código fora do vocabulário fechado observado em dado real não é promovido pelo backfill — limite " +
            "documentado, não uma omissão silenciosa");
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
    /// Semeia, em SQL cru, um processo e três fatos coletados como o modelo ANTIGO gravava — sem
    /// as colunas <c>rotulo</c>/<c>tipo_renderizacao</c>/<c>obrigatorio</c>, que só a migration
    /// desta story cria (o <c>AddColumn</c> as preenche com <c>''</c>/<c>0</c>/<c>false</c> antes
    /// do backfill rodar).
    /// </summary>
    private async Task SemearFatoColetadoLegadoAsync()
    {
        await using NpgsqlConnection conexao = new(_postgres.GetConnectionString());
        await conexao.OpenAsync();

        await ExecutarAsync(conexao, $$"""
            INSERT INTO selecao.processos_seletivos (id, nome, tipo, status, created_at, is_deleted)
            VALUES ('{{ProcessoId}}', 'PS legado', 1, 1, now(), false);

            INSERT INTO selecao.fatos_coletados (id, processo_seletivo_id, fato_codigo, ordem, created_at)
            VALUES
                ('{{CorRacaId}}', '{{ProcessoId}}', 'COR_RACA', 0, now()),
                ('{{BaixaRendaId}}', '{{ProcessoId}}', 'BAIXA_RENDA', 1, now()),
                ('{{OutroFatoId}}', '{{ProcessoId}}', 'OUTRO_FATO_QUALQUER', 2, now());
            """);
    }

    private static async Task ExecutarAsync(NpgsqlConnection conexao, string sql)
    {
        await using NpgsqlCommand comando = new(sql, conexao);
        await comando.ExecuteNonQueryAsync();
    }
}
