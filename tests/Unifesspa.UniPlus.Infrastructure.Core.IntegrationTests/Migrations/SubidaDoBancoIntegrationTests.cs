namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Migrations;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Monolito.Persistencia;

/// <summary>
/// A subida do banco do contexto único: scripts <c>Pre</c>, migration e scripts pós, sob a trava do banco, aplicados em
/// bancos vazios e descartáveis dentro do mesmo servidor.
/// </summary>
public sealed class SubidaDoBancoIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    // Constraints, índices, gatilhos e funções dos esquemas da aplicação: se reaplicar mudasse algo, a impressão mudaria.
    private const string SqlDaImpressaoDoCatalogo =
        """
        SELECT md5(string_agg(item, '|' ORDER BY item)) FROM (
            SELECT 'c:' || conrelid::regclass::text || conname || pg_get_constraintdef(oid) AS item FROM pg_constraint
                WHERE connamespace::regnamespace::text NOT LIKE 'pg\_%' AND connamespace::regnamespace::text <> 'information_schema'
            UNION ALL SELECT 'i:' || indexdef FROM pg_indexes WHERE schemaname NOT IN ('pg_catalog', 'information_schema')
            UNION ALL SELECT 't:' || pg_get_triggerdef(oid) FROM pg_trigger WHERE NOT tgisinternal
            UNION ALL SELECT 'f:' || pg_get_functiondef(p.oid) FROM pg_proc p
                WHERE p.pronamespace::regnamespace::text NOT IN ('pg_catalog', 'information_schema') AND p.prokind = 'f'
                AND NOT EXISTS (SELECT 1 FROM pg_depend d WHERE d.objid = p.oid AND d.deptype = 'e')
        ) itens
        """;

    private int _bancos;

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact(DisplayName = "A subida do banco vazio cria o que cada fase declara: extensão, função da plataforma, restrição do módulo e um só histórico")]
    public async Task SubirOBanco_BancoVazio_AplicaTodasAsFases()
    {
        string conexao = await NovoBancoAsync();

        await SubirAsync(conexao);

        (await EscalarAsync(conexao, "SELECT count(*) FROM pg_extension WHERE extname = 'btree_gist'")).Should().Be(1);
        (await EscalarAsync(conexao, "SELECT count(*) FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace WHERE n.nspname = 'plataforma' AND p.proname = 'normalizar_para_comparacao'")).Should().Be(1);
        (await EscalarAsync(conexao, "SELECT count(*) FROM pg_constraint WHERE conname = 'ex_tipo_ato_publicado_codigo_vigencia'")).Should().Be(1);
        (await EscalarAsync(conexao, "SELECT count(*) FROM plataforma.\"__EFMigrationsHistory\"")).Should().Be(1);
    }

    [Fact(DisplayName = "Reaplicar a subida num banco já subido não altera o catálogo")]
    public async Task SubirOBanco_Reaplicado_NaoAlteraOCatalogo()
    {
        string conexao = await NovoBancoAsync();
        await SubirAsync(conexao);
        string antes = await ImpressaoDoCatalogoAsync(conexao);

        await SubirAsync(conexao);

        (await ImpressaoDoCatalogoAsync(conexao)).Should().Be(antes);
    }

    [Fact(DisplayName = "Duas réplicas que sobem o mesmo banco ao mesmo tempo terminam sem erro")]
    public async Task SubirOBanco_DuasReplicasAoMesmoTempo_TerminamSemErro()
    {
        string conexao = await NovoBancoAsync();

        Func<Task> subidas = () => Task.WhenAll(SubirAsync(conexao), SubirAsync(conexao));

        await subidas.Should().NotThrowAsync();
        (await EscalarAsync(conexao, "SELECT count(*) FROM plataforma.\"__EFMigrationsHistory\"")).Should().Be(1);
    }

    [Theory(DisplayName = "A função de normalização do banco devolve o mesmo texto que a normalização da aplicação")]
    [InlineData("Ação")]
    [InlineData("ÇÃO Ñandú ÀÉÎÕÜ")]
    [InlineData("Edital 01/2027 — Processo Seletivo")]
    public async Task FuncaoDeNormalizacao_NoBanco_ConcordaComAAplicacao(string texto)
    {
        string conexao = await NovoBancoAsync();
        await SubirAsync(conexao);

        string? doBanco = await TextoAsync(conexao, texto);

        doBanco.Should().Be(NormalizacaoTextual.Normalizar(texto));
    }

    private static async Task SubirAsync(string conexao)
    {
        DbContextOptions<MonolitoDbContext> opcoes = new DbContextOptionsBuilder<MonolitoDbContext>()
            .UseNpgsql(conexao, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", UniPlusDbContext.EsquemaDaPlataforma))
            .UseSnakeCaseNamingConvention()
            .Options;

        await using MonolitoDbContext contexto = new(opcoes);
        await contexto.SubirOBancoAsync();
    }

    private async Task<string> NovoBancoAsync()
    {
        string nome = $"subida_{++_bancos}";
        await using NpgsqlConnection admin = new(_postgres.GetConnectionString());
        await admin.OpenAsync();
        await using NpgsqlCommand criar = new($"CREATE DATABASE {nome}", admin);
        await criar.ExecuteNonQueryAsync();

        return new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString()) { Database = nome }.ConnectionString;
    }

    private static async Task<long> EscalarAsync(string conexao, string sql)
    {
        await using NpgsqlConnection banco = new(conexao);
        await banco.OpenAsync();
        await using NpgsqlCommand comando = new(sql, banco);
        return (long)(await comando.ExecuteScalarAsync())!;
    }

    private static async Task<string?> TextoAsync(string conexao, string texto)
    {
        await using NpgsqlConnection banco = new(conexao);
        await banco.OpenAsync();
        await using NpgsqlCommand comando = new("SELECT plataforma.normalizar_para_comparacao($1)", banco);
        comando.Parameters.Add(new NpgsqlParameter { Value = texto });
        return (string?)await comando.ExecuteScalarAsync();
    }

    private static async Task<string> ImpressaoDoCatalogoAsync(string conexao)
    {
        await using NpgsqlConnection banco = new(conexao);
        await banco.OpenAsync();
        await using NpgsqlCommand comando = new(SqlDaImpressaoDoCatalogo, banco);
        return (string)(await comando.ExecuteScalarAsync())!;
    }
}
