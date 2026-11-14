namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Migrations;

using System.Data.Common;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Npgsql;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Monolito.Persistencia;

/// <summary>
/// A preparação de um banco que ainda não existe: a verificação e a criação acontecem antes da trava do banco, então
/// outra réplica pode criá-lo entre as duas. Quem perde a corrida segue, e qualquer outra falha da criação propaga.
/// </summary>
public sealed class PreparacaoDoBancoInexistenteIntegrationTests : IAsyncLifetime
{
    private const string SqlDoHistorico = "SELECT count(*) FROM plataforma.\"__EFMigrationsHistory\"";
    private const string SenhaDoPapel = "sem_permissao";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    private int _bancos;

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact(DisplayName = "Outra réplica cria o banco entre a verificação e a criação: a preparação segue e termina sem erro")]
    public async Task PrepararOBanco_OutraReplicaCriaOBancoAntes_SegueSemErro()
    {
        string conexao = NomeDeBancoInexistente();
        OutraReplicaCriaOBancoAoFalharAPrimeiraAbertura outraReplica = new(_postgres.GetConnectionString(), conexao);

        Func<Task> preparacao = () => PrepararAsync(conexao, outraReplica);

        await preparacao.Should().NotThrowAsync();
        outraReplica.CriouOBanco.Should().BeTrue();
        (await EscalarAsync(conexao, SqlDoHistorico)).Should().Be(1);
    }

    [Fact(DisplayName = "Duas réplicas que preparam o mesmo banco inexistente ao mesmo tempo terminam sem erro")]
    public async Task PrepararOBanco_DuasReplicasComBancoInexistente_TerminamSemErro()
    {
        string conexao = NomeDeBancoInexistente();

        Func<Task> preparacoes = () => Task.WhenAll(PrepararAsync(conexao), PrepararAsync(conexao));

        await preparacoes.Should().NotThrowAsync();
        (await EscalarAsync(conexao, SqlDoHistorico)).Should().Be(1);
    }

    [Fact(DisplayName = "A falha de criação que não é de banco já existente propaga")]
    public async Task PrepararOBanco_PapelSemPermissaoParaCriar_Propaga()
    {
        await CriarPapelSemPermissaoAsync();
        string conexao = new NpgsqlConnectionStringBuilder(NomeDeBancoInexistente())
        {
            Username = "sem_permissao",
            Password = SenhaDoPapel,
        }.ConnectionString;

        Func<Task> preparacao = () => PrepararAsync(conexao);

        PostgresException falha = (await preparacao.Should().ThrowAsync<PostgresException>()).Which;
        falha.SqlState.Should().Be(PostgresErrorCodes.InsufficientPrivilege);
    }

    private static async Task PrepararAsync(string conexao, DbConnectionInterceptor? interceptador = null)
    {
        DbContextOptionsBuilder<MonolitoDbContext> construtor = new DbContextOptionsBuilder<MonolitoDbContext>()
            .UseNpgsql(conexao, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", UniPlusDbContext.EsquemaDaPlataforma))
            .UseSnakeCaseNamingConvention();
        if (interceptador is not null)
        {
            construtor.AddInterceptors(interceptador);
        }

        await using MonolitoDbContext contexto = new(construtor.Options);
        await contexto.PrepararOBancoAsync();
    }

    private string NomeDeBancoInexistente() =>
        new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString()) { Database = $"inexistente_{++_bancos}" }
            .ConnectionString;

    private async Task CriarPapelSemPermissaoAsync()
    {
        await using NpgsqlConnection admin = new(_postgres.GetConnectionString());
        await admin.OpenAsync();
        await using NpgsqlCommand criar = new($"CREATE ROLE sem_permissao LOGIN PASSWORD '{SenhaDoPapel}'", admin);
        await criar.ExecuteNonQueryAsync();
    }

    private static async Task<long> EscalarAsync(string conexao, string sql)
    {
        await using NpgsqlConnection banco = new(conexao);
        await banco.OpenAsync();
        await using NpgsqlCommand comando = new(sql, banco);
        return (long)(await comando.ExecuteScalarAsync())!;
    }

    // A verificação do EF abre a conexão e interpreta a falha de banco ausente como "não existe": criar o banco nesse
    // instante reproduz a outra réplica vencendo a corrida antes da criação.
    private sealed class OutraReplicaCriaOBancoAoFalharAPrimeiraAbertura(string conexaoDoServidor, string conexaoDoBanco)
        : DbConnectionInterceptor
    {
        public bool CriouOBanco { get; private set; }

        public override async Task ConnectionFailedAsync(
            DbConnection connection,
            ConnectionErrorEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (CriouOBanco)
            {
                return;
            }

            CriouOBanco = true;
            string nome = new NpgsqlConnectionStringBuilder(conexaoDoBanco).Database!;
            await using NpgsqlConnection admin = new(conexaoDoServidor);
            await admin.OpenAsync(cancellationToken);
            await using NpgsqlCommand criar = new($"CREATE DATABASE {nome}", admin);
            await criar.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
