namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposDocumento;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

/// <summary>
/// Prova que a migration que fecha o formato do código atravessa um banco que já
/// tem dado gravado sob a regra antiga — e não um banco vazio, que é o estado em
/// que a fixture compartilhada sempre nasce.
/// </summary>
/// <remarks>
/// <para>É a ordem que está sob teste. O conversor recusa reidratar código fora do
/// formato, e o CHECK recusa mantê-lo na tabela: se a reescrita dos dados não
/// acontecesse antes das alterações de schema, a própria migration abortaria com
/// <c>23514</c>, e um deploy que passasse dela deixaria toda leitura do cadastro
/// respondendo <c>500</c> por causa de uma única linha.</para>
/// <para>As linhas semeadas reproduzem o cadastro real de homologação, que foi
/// preenchido com códigos sequenciais, incluindo uma soft-deleted: o índice único
/// parcial a ignora, mas o CHECK e o <c>NOT NULL</c> valem para a tabela inteira.</para>
/// </remarks>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL fixo de teste; os valores interpolados são Guid gerados localmente.")]
[SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Recursos liberados por IAsyncLifetime.DisposeAsync — xUnit invoca deterministicamente.")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class MigracaoCodigoTipoDocumentoTests : IAsyncLifetime
{
    private const string MigrationAnterior = "20260831002351_RemoveDominioFechadoDaCategoriaDoTipoDocumento";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_migracao_codigo_tipo_documento_tests")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync().ConfigureAwait(false);

    public async Task DisposeAsync() => await _postgres.DisposeAsync().ConfigureAwait(false);

    [Fact(DisplayName = "Migration atravessa cadastro com códigos sequenciais e deixa a leitura sã")]
    public async Task Migration_ComCodigoLegado_LimpaAntesDeFecharOFormato()
    {
        await using (ConfiguracaoDbContext contextoLegado = CriarContexto())
        {
            IMigrator migrator = contextoLegado.GetService<IMigrator>();
            await migrator.MigrateAsync(MigrationAnterior);
        }

        await SemearCodigosSequenciaisAsync();

        await using (ConfiguracaoDbContext contextoNovo = CriarContexto())
        {
            Func<Task> migrar = async () => await contextoNovo.Database.MigrateAsync();

            await migrar.Should().NotThrowAsync(
                "a reescrita dos dados precede as alterações de schema — sem ela o CHECK de formato abortaria a migration");
        }

        await using (ConfiguracaoDbContext contextoLeitura = CriarContexto())
        {
            List<TipoDocumento> lidos = await contextoLeitura.TiposDocumento
                .AsNoTracking()
                .IgnoreQueryFilters()
                .ToListAsync();

            lidos.Should().BeEmpty(
                "nenhum código sequencial sobrevive — nem o soft-deleted, que o índice único ignora mas o CHECK não");
        }
    }

    private ConfiguracaoDbContext CriarContexto()
    {
        DbContextOptions<ConfiguracaoDbContext> options = new DbContextOptionsBuilder<ConfiguracaoDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ConfiguracaoDbContext(options);
    }

    private async Task SemearCodigosSequenciaisAsync()
    {
        await using NpgsqlConnection conexao = new(_postgres.GetConnectionString());
        await conexao.OpenAsync();

        (string Codigo, string Nome, string Categoria, bool Excluido)[] legado =
        [
            ("01", "Certificado", "ESCOLARIDADE", false),
            ("08", "LAUDO MÉDICO", "SAUDE", false),
            ("16", "RG", "IDENTIFICACAO", false),
            ("99", "Tipo abandonado", "OUTROS", true),
        ];

        foreach ((string codigo, string nome, string categoria, bool excluido) in legado)
        {
            await using NpgsqlCommand comando = new(
                $$"""
                INSERT INTO configuracao.tipo_documento
                    (id, codigo, nome, categoria, created_at, is_deleted)
                VALUES ('{{Guid.CreateVersion7()}}', '{{codigo}}', '{{nome}}', '{{categoria}}', now(), {{excluido.ToString().ToUpperInvariant()}});
                """,
                conexao);
            await comando.ExecuteNonQueryAsync();
        }
    }
}
