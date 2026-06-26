namespace Unifesspa.UniPlus.Geo.IntegrationTests;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using JasperFx.CodeGeneration.Model;

using Unifesspa.UniPlus.Geo.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Guarda de regressão da política de service location do codegen Wolverine
/// (ADR-0098) para o host Geo. As queries do Geo injetam readers cujos tipos
/// concretos eram <c>internal</c> — corrigidos na RAIZ (tornados <c>public</c>)
/// para que o codegen os construa inline sob <c>ServiceLocationPolicy.NotAllowed</c>.
/// </summary>
/// <remarks>
/// A UoW base do Geo (<c>IUnitOfWork</c>) não é injetada em nenhum handler (as
/// cargas ETL rodam em hosted services), então não exige opt-in. A mecânica vive em
/// <see cref="ServiceLocationCodegenGuard"/>.
/// </remarks>
[Collection(GeoPostgisCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class ServiceLocationGuardTests
{
    private readonly GeoPostgisFixture _fixture;

    public ServiceLocationGuardTests(GeoPostgisFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Política de service location está travada em NotAllowed (ADR-0098)")]
    public void Politica_TravadaEm_NotAllowed()
    {
        ServiceLocationCodegenGuard.PoliticaEfetiva(_fixture.Factory.Services).Should().Be(
            ServiceLocationPolicy.NotAllowed,
            "o WolverineOutboxConfiguration trava a política para forward-compat com o "
            + "Wolverine 6.0 e para que esta guarda detecte regressões (ADR-0098)");
    }

    [Fact(DisplayName = "Nenhuma chain do Geo dispara service location sem opt-in (ADR-0098)")]
    public async Task NenhumaChain_DisparaServiceLocation()
    {
        ServiceLocationVarredura resultado =
            await ServiceLocationCodegenGuard.VarrerAsync(_fixture.Factory.Services, "Unifesspa.UniPlus.Geo");

        resultado.Verificadas.Should().BeGreaterThan(0, "o host Geo tem queries CQRS a guardar");
        resultado.Ofensores.Should().BeEmpty(
            "toda chain de handler deve gerar código sem service location sob NotAllowed; "
            + "torne o tipo concreto público (root fix) ou declare AlwaysUseServiceLocationFor<T>() "
            + "se o forwarding for obrigatório");
    }
}
