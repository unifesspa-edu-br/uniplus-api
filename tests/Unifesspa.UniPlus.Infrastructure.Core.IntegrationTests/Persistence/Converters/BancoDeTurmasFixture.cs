namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Persistence.Converters;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Testcontainers.PostgreSql;

/// <summary>
/// Um PostgreSQL real, com a tabela das turmas criada pelo modelo do EF, compartilhado pelos testes da classe: cada teste
/// grava e lê as próprias linhas, identificadas por um <see cref="Guid"/> novo.
/// </summary>
public sealed class BancoDeTurmasFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_codigos_de_contrato_tests")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using ContextoDeTurmas contexto = CriarContexto();
        await contexto.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    internal ContextoDeTurmas CriarContexto() =>
        new(new DbContextOptionsBuilder<ContextoDeTurmas>().UseNpgsql(_postgres.GetConnectionString()).Options);

    internal async Task<NpgsqlConnection> AbrirConexaoAsync()
    {
        NpgsqlConnection conexao = new(_postgres.GetConnectionString());
        await conexao.OpenAsync();

        return conexao;
    }
}
