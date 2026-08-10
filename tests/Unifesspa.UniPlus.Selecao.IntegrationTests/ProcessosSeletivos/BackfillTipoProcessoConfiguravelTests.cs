namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Prova que a migration de tipos configuráveis preserva a identidade dos oito valores do
/// enum legado por meio dos mesmos UUIDv7 determinísticos semeados em Configuração.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL fixo de teste; os valores interpolados são inteiros ou Guid gerados localmente.")]
[SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Recursos liberados por IAsyncLifetime.DisposeAsync — xUnit invoca deterministicamente.")]
public sealed class BackfillTipoProcessoConfiguravelTests : IAsyncLifetime
{
    private const string MigrationAnterior = "20260805145623_AddConfiguracaoDivulgacao";

    private static readonly (int Tipo, Guid OrigemId, string Codigo)[] MapeamentoEsperado =
    [
        (1, new Guid("019fe8f8-1400-7000-8000-000000000001"), "SiSU"),
        (2, new Guid("019fe8f8-1400-7000-8000-000000000002"), "PSIQ"),
        (3, new Guid("019fe8f8-1400-7000-8000-000000000003"), "PSECampo"),
        (4, new Guid("019fe8f8-1400-7000-8000-000000000004"), "PSVR"),
        (5, new Guid("019fe8f8-1400-7000-8000-000000000005"), "TransferenciaInterna"),
        (6, new Guid("019fe8f8-1400-7000-8000-000000000006"), "TransferenciaExterna"),
        (7, new Guid("019fe8f8-1400-7000-8000-000000000007"), "PortadorDiploma"),
        (8, new Guid("019fe8f8-1400-7000-8000-000000000008"), "Reopcao"),
    ];

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_backfill_tipo_processo_tests")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync().ConfigureAwait(false);

    public async Task DisposeAsync() => await _postgres.DisposeAsync().ConfigureAwait(false);

    [Fact(DisplayName = "Backfill converte os oito tipos legados para os UUIDv7 determinísticos de Configuração")]
    public async Task Migration_ConverteTodosOsTiposLegadosParaUuidV7Correspondente()
    {
        await using (SelecaoDbContext contextoLegado = CriarContexto())
        {
            IMigrator migrator = contextoLegado.GetService<IMigrator>();
            await migrator.MigrateAsync(MigrationAnterior);
            await SemearProcessosLegadosAsync();
        }

        await using (SelecaoDbContext contextoNovo = CriarContexto())
        {
            await contextoNovo.Database.MigrateAsync();
        }

        IReadOnlyDictionary<string, Guid> idsPorCodigo = await LerIdsPorCodigoAsync();

        idsPorCodigo.Should().HaveCount(8);
        foreach ((_, Guid origemId, string codigo) in MapeamentoEsperado)
        {
            idsPorCodigo.Should().ContainKey(codigo);
            idsPorCodigo[codigo].Should().Be(origemId);
            EhUuidV7Rfc9562(idsPorCodigo[codigo]).Should().BeTrue(
                $"o identificador congelado de {codigo} deve respeitar ADR-0032");
        }
    }

    private SelecaoDbContext CriarContexto()
    {
        DbContextOptions<SelecaoDbContext> options = new DbContextOptionsBuilder<SelecaoDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;

        return new SelecaoDbContext(options);
    }

    private async Task SemearProcessosLegadosAsync()
    {
        await using NpgsqlConnection conexao = new(_postgres.GetConnectionString());
        await conexao.OpenAsync();

        foreach ((int tipo, _, _) in MapeamentoEsperado)
        {
            await using NpgsqlCommand comando = new(
                $$"""
                INSERT INTO selecao.processos_seletivos (id, nome, tipo, status, created_at, is_deleted)
                VALUES ('{{Guid.CreateVersion7()}}', 'PS legado {{tipo}}', {{tipo}}, 1, now(), false);
                """,
                conexao);
            await comando.ExecuteNonQueryAsync();
        }
    }

    private async Task<IReadOnlyDictionary<string, Guid>> LerIdsPorCodigoAsync()
    {
        await using NpgsqlConnection conexao = new(_postgres.GetConnectionString());
        await conexao.OpenAsync();
        await using NpgsqlCommand comando = new(
            "SELECT tipo_processo_codigo, tipo_processo_origem_id FROM selecao.processos_seletivos",
            conexao);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync();
        Dictionary<string, Guid> resultado = new(StringComparer.Ordinal);

        while (await leitor.ReadAsync())
        {
            resultado.Add(leitor.GetString(0), leitor.GetGuid(1));
        }

        return resultado;
    }

    private static bool EhUuidV7Rfc9562(Guid id)
    {
        string representacao = id.ToString("D");
        return id.Version == 7 && representacao[19] is '8' or '9' or 'a' or 'b';
    }
}
