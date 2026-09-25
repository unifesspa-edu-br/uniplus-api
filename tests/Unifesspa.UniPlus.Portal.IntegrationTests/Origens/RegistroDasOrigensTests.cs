namespace Unifesspa.UniPlus.Portal.IntegrationTests.Origens;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Portal.Application.Certames;

[Trait("Category", "Integration")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IClassFixture<T> exige tipo de teste público.")]
public sealed class RegistroDasOrigensTests : IClassFixture<PortalApiFactory>
{
    private readonly PortalApiFactory _comOrigens;

    public RegistroDasOrigensTests(PortalApiFactory comOrigens)
    {
        _comOrigens = comOrigens;
    }

    [Fact(DisplayName = "Com as origens configuradas, a Portal sobe e monta a composição da página do certame")]
    public void ComOrigens_ComposicaoResolvida()
    {
        using IServiceScope escopo = _comOrigens.Services.CreateScope();

        escopo.ServiceProvider.GetService<ComposicaoDaPaginaDoCertame>().Should().NotBeNull();
    }

    [Fact(DisplayName = "Sem os endereços das origens, a Portal recusa subir")]
    public void SemOrigens_RecusaSubir()
    {
        using PortalSemOrigensApiFactory semOrigens = new();

        Action subir = () => semOrigens.CreateClient().Dispose();

        subir.Should().Throw<OptionsValidationException>()
            .Which.Message.Should().Contain("SelecaoBaseUrl").And.Contain("PublicacoesBaseUrl");
    }
}
