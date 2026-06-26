namespace Unifesspa.UniPlus.Host.IntegrationTests;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;

/// <summary>
/// Prova de boot do monólito modular (P4 em runtime): o composition root sobe
/// como um processo único e, no <c>StartAsync</c>, as migrations on startup dos
/// 4 módulos criam seus schemas no banco <c>uniplus</c> e o Wolverine provisiona
/// o outbox no schema <c>wolverine</c> — tudo sobre a MESMA connection.
/// </summary>
/// <remarks>
/// O fato de a fixture conseguir resolver serviços do host (boot bem-sucedido) já
/// prova que os 4 <c>MigrationHostedService</c> e o runtime Wolverine iniciaram
/// sem conflito (o conflito de 4 <c>Program</c> executáveis NÃO ocorre — o entry
/// point é único). Este teste fecha o ciclo afirmando que os schemas esperados
/// materializaram.
/// </remarks>
[Collection(MonolitoHostCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class BootDoMonolitoTests
{
    private readonly MonolitoHostFixture _fixture;

    public BootDoMonolitoTests(MonolitoHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory(DisplayName = "Boot do monólito cria os 4 schemas de módulo + wolverine no banco único")]
    [InlineData("configuracao")]
    [InlineData("organizacao")]
    [InlineData("selecao")]
    [InlineData("ingresso")]
    [InlineData("wolverine")]
    public async Task Boot_CriaSchemaEsperado(string schema)
    {
        IReadOnlyCollection<string> schemas = await ListarSchemasAsync();

        schemas.Should().Contain(
            schema,
            "as migrations on startup (módulos) e o AutoBuildMessageStorageOnStartup (Wolverine) "
            + "devem materializar o schema no boot do processo único");
    }

    [Fact(DisplayName = "Schemas de módulo coexistem no MESMO banco (schema-por-módulo)")]
    public async Task Boot_SchemasDeModuloCoexistemNoMesmoBanco()
    {
        IReadOnlyCollection<string> schemas = await ListarSchemasAsync();

        schemas.Should().Contain(["configuracao", "organizacao", "selecao", "ingresso"],
            "os 4 módulos compartilham o banco `uniplus`, isolados por schema (não por banco)");
    }

    private async Task<IReadOnlyCollection<string>> ListarSchemasAsync()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        OrganizacaoInstitucionalDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<OrganizacaoInstitucionalDbContext>();

        return await dbContext.Database
            .SqlQueryRaw<string>("SELECT schema_name AS \"Value\" FROM information_schema.schemata")
            .ToListAsync(CancellationToken.None);
    }
}
