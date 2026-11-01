namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FatosCandidato;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// A migration que torna o nome social pessoal atravessa um banco que ainda tem a semente antiga (pública) e o deixa
/// pessoal: a linha é reescrita antes de a restrição nova ser criada, senão a restrição falharia na linha antiga.
/// </summary>
[SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Recursos liberados por IAsyncLifetime.DisposeAsync — xUnit invoca deterministicamente.")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class MigracaoNomeSocialPessoalTests : IAsyncLifetime
{
    private const string MigrationAnterior = "20261101181535_RegistraOsAtosDaProtecaoDoCatalogo";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_migracao_nome_social_tests")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync().ConfigureAwait(false);

    public async Task DisposeAsync() => await _postgres.DisposeAsync().ConfigureAwait(false);

    [Fact(DisplayName = "Banco com a semente antiga: o nome social é público antes e pessoal depois, e a restrição nova aceita a linha")]
    public async Task Migration_ComSementeAntiga_DeixaONomeSocialPessoal()
    {
        await using (ConfiguracaoDbContext anterior = CriarContexto())
        {
            await anterior.GetService<IMigrator>().MigrateAsync(MigrationAnterior);
        }

        (await ClassificacaoDoNomeSocialAsync()).Should().Be("PUBLICO");

        await using (ConfiguracaoDbContext contexto = CriarContexto())
        {
            Func<Task> migrar = async () => await contexto.Database.MigrateAsync();
            await migrar.Should().NotThrowAsync("o UpdateData da linha vem antes da restrição recriada");
        }

        (await ClassificacaoDoNomeSocialAsync()).Should().Be("PESSOAL");
    }

    [Fact(DisplayName = "Banco novo: o nome social nasce pessoal, e a restrição recusa o nome social público")]
    public async Task Migration_BancoNovo_NomeSocialNascePessoal()
    {
        await using (ConfiguracaoDbContext contexto = CriarContexto())
        {
            await contexto.Database.MigrateAsync();
        }

        (await ClassificacaoDoNomeSocialAsync()).Should().Be("PESSOAL");

        await using NpgsqlConnection conexao = new(_postgres.GetConnectionString());
        await conexao.OpenAsync();
        await using NpgsqlCommand publico = new(
            $"UPDATE configuracao.rol_de_fatos_candidato SET classificacao_protecao = 'PUBLICO' WHERE codigo = '{FatoCandidato.CodigoDoNomeSocial}'",
            conexao);
        Func<Task> tornarPublico = async () => await publico.ExecuteNonQueryAsync();

        (await tornarPublico.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("23514");
    }

    private async Task<string?> ClassificacaoDoNomeSocialAsync()
    {
        await using NpgsqlConnection conexao = new(_postgres.GetConnectionString());
        await conexao.OpenAsync();
        await using NpgsqlCommand consulta = new(
            $"SELECT classificacao_protecao FROM configuracao.rol_de_fatos_candidato WHERE codigo = '{FatoCandidato.CodigoDoNomeSocial}'",
            conexao);

        return (string?)await consulta.ExecuteScalarAsync();
    }

    private ConfiguracaoDbContext CriarContexto()
    {
        DbContextOptions<ConfiguracaoDbContext> options = new DbContextOptionsBuilder<ConfiguracaoDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ConfiguracaoDbContext(options);
    }
}
