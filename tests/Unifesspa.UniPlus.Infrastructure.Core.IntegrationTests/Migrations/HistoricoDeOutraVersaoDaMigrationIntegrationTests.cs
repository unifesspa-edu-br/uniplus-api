namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Migrations;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

/// <summary>
/// O banco que tem migration aplicada desconhecida do código e ainda não tem a deste código foi criado por outra
/// versão da migration: a subida recusa antes de aplicar. Sem pendentes, a desconhecida é volta de imagem e passa.
/// </summary>
public sealed class HistoricoDeOutraVersaoDaMigrationIntegrationTests : IAsyncLifetime
{
    private const string IdAntigo = "20200101000000_VersaoAntiga";
    private const string IdAtual = "20200102000000_VersaoAtual";
    private const string IdPosterior = "20200103000000_VersaoPosterior";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    private int _bancos;

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact(DisplayName = "Migration aplicada desconhecida e migration do código pendente: a subida recusa e não aplica nada")]
    public async Task ApplyMigrationsAsync_DesconhecidaAplicadaEPendente_RecusaSemAplicar()
    {
        string conexao = NovaConexao();
        await AplicarAsync<ContextoDaVersaoAntiga>(conexao);

        Func<Task> acao = () => AplicarAsync<ContextoDaVersaoAtual>(conexao);

        (await acao.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain(IdAntigo).And.Contain(IdAtual);
        (await HistoricoAsync(conexao)).Should().Equal(IdAntigo);
    }

    [Fact(DisplayName = "Migration aplicada desconhecida sem migration pendente: a subida segue sem erro")]
    public async Task ApplyMigrationsAsync_DesconhecidaAplicadaSemPendente_NaoRecusa()
    {
        string conexao = NovaConexao();
        await AplicarAsync<ContextoDaVersaoAtual>(conexao);
        await RegistrarComoAplicadaAsync(conexao, IdPosterior);

        Func<Task> acao = () => AplicarAsync<ContextoDaVersaoAtual>(conexao);

        await acao.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "Banco vazio: a subida aplica a migration do código")]
    public async Task ApplyMigrationsAsync_BancoVazio_AplicaAMigrationDoCodigo()
    {
        string conexao = NovaConexao();

        await AplicarAsync<ContextoDaVersaoAtual>(conexao);

        (await HistoricoAsync(conexao)).Should().Equal(IdAtual);
    }

    private static async Task AplicarAsync<TContext>(string conexao)
        where TContext : DbContext
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddDbContext<TContext>(opts => opts.UseNpgsql(conexao));

        ServiceProvider provider = services.BuildServiceProvider();
        await using (provider.ConfigureAwait(false))
        {
            await provider.ApplyMigrationsAsync<TContext>();
        }
    }

    private static async Task RegistrarComoAplicadaAsync(string conexao, string migrationId)
    {
        await using NpgsqlConnection banco = new(conexao);
        await banco.OpenAsync();
        await using NpgsqlCommand comando = new(
            "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES (@id, '10.0.0')", banco);
        comando.Parameters.AddWithValue("id", migrationId);
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<string[]> HistoricoAsync(string conexao)
    {
        DbContextOptions<ContextoDaVersaoAtual> opcoes = new DbContextOptionsBuilder<ContextoDaVersaoAtual>()
            .UseNpgsql(conexao)
            .Options;
        await using ContextoDaVersaoAtual contexto = new(opcoes);
        return [.. await contexto.Database.GetAppliedMigrationsAsync()];
    }

    private string NovaConexao() =>
        new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        {
            Database = $"historico_de_outra_versao_{++_bancos}",
        }.ConnectionString;

    private sealed class ContextoDaVersaoAntiga(DbContextOptions<ContextoDaVersaoAntiga> options) : DbContext(options);

    private sealed class ContextoDaVersaoAtual(DbContextOptions<ContextoDaVersaoAtual> options) : DbContext(options);

    [DbContext(typeof(ContextoDaVersaoAntiga))]
    [Migration(IdAntigo)]
    private sealed class MigrationDaVersaoAntiga : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql("CREATE TABLE IF NOT EXISTS tabela_existente (id integer)");
    }

    [DbContext(typeof(ContextoDaVersaoAtual))]
    [Migration(IdAtual)]
    private sealed class MigrationDaVersaoAtual : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql("CREATE TABLE tabela_existente (id integer)");
    }
}
