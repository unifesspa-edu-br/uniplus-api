namespace Unifesspa.UniPlus.Portal.IntegrationTests.Origens;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Infrastructure;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Portal.Application.Certames;

[Trait("Category", "Integration")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IClassFixture<T> exige tipo de teste público.")]
public sealed class RegistroDasOrigensTests :
    IClassFixture<PortalComOrigensApiFactory>,
    IClassFixture<PortalApiFactory>
{
    private readonly PortalComOrigensApiFactory _comOrigens;
    private readonly PortalApiFactory _semOrigens;

    public RegistroDasOrigensTests(PortalComOrigensApiFactory comOrigens, PortalApiFactory semOrigens)
    {
        _comOrigens = comOrigens;
        _semOrigens = semOrigens;
    }

    [Fact(DisplayName = "Com as origens configuradas, a Portal sobe e monta a composição da página do certame")]
    public void ComOrigens_ComposicaoResolvida()
    {
        using IServiceScope escopo = _comOrigens.Services.CreateScope();

        escopo.ServiceProvider.GetService<ComposicaoDaPaginaDoCertame>().Should().NotBeNull();
    }

    [Fact(DisplayName = "Sem as origens configuradas, a Portal sobe sem a composição")]
    public void SemOrigens_ComposicaoAusente()
    {
        using IServiceScope escopo = _semOrigens.Services.CreateScope();

        escopo.ServiceProvider.GetService<ComposicaoDaPaginaDoCertame>().Should().BeNull();
    }
}
