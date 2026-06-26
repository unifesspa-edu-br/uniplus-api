namespace Unifesspa.UniPlus.Host.IntegrationTests;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using JasperFx.CodeGeneration.Model;

using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Guarda de regressão da política de service location do codegen Wolverine
/// (ADR-0098) para o host monólito (módulos Seleção, Configuração, Organização
/// Institucional e Ingresso). Trava o forward-compat com o
/// <c>ServiceLocationPolicy.NotAllowed</c> que vira default no Wolverine 6.0:
/// força a geração de código de TODA chain CQRS conhecida e falha de forma
/// legível, nomeando o tipo ofensor, se alguma exigir service location (lambda
/// factory opaca ou tipo concreto não-público) sem opt-in explícito.
/// </summary>
/// <remarks>
/// Cada módulo declara os opt-ins justificados (UoW que encaminham para a MESMA
/// instância de DbContext — ADR-0004) via <c>AlwaysUseServiceLocationFor&lt;T&gt;()</c>
/// no seu <c>*CodegenRegistration</c>. Uma nova dependência opaca que vaze para um
/// handler sem opt-in passa a quebrar este teste em vez de cair silenciosamente em
/// service location no runtime. A mecânica vive em
/// <see cref="ServiceLocationCodegenGuard"/>.
/// </remarks>
[Collection(ServiceLocationGuardCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class ServiceLocationGuardTests
{
    private readonly ServiceLocationGuardFixture _fixture;

    public ServiceLocationGuardTests(ServiceLocationGuardFixture fixture)
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

    [Fact(DisplayName = "Nenhuma chain do monólito dispara service location sem opt-in (ADR-0098)")]
    public async Task NenhumaChain_DisparaServiceLocation()
    {
        ServiceLocationVarredura resultado =
            await ServiceLocationCodegenGuard.VarrerAsync(_fixture.Factory.Services, "Unifesspa.UniPlus");

        resultado.Verificadas.Should().BeGreaterThan(0, "o host monólito tem chains CQRS a guardar");
        resultado.Ofensores.Should().BeEmpty(
            "toda chain de handler deve gerar código sem service location sob NotAllowed; "
            + "corrija na raiz (registre concreto público / AddScoped<TInterface, TImpl>) ou, "
            + "se o forwarding for obrigatório (UoW → mesma instância de DbContext, ADR-0004), "
            + "declare AlwaysUseServiceLocationFor<T>() no *CodegenRegistration do módulo");
    }
}
