namespace Unifesspa.UniPlus.Portal.IntegrationTests.Saude;

using System.Diagnostics.CodeAnalysis;
using System.Net;

using AwesomeAssertions;

using Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

/// <summary>
/// A prontidão da Portal responde por quem ela serve (ADR-0131): entra o que alguma rota servida
/// precisa para responder. Hoje as rotas são sessão, perfil e o ping; só o provedor de identidade
/// é exercido por elas.
/// </summary>
[Trait("Category", "Integration")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IClassFixture<T> exige tipo de teste público.")]
public sealed class ProntidaoDaPortalTests : IClassFixture<PortalProntidaoApiFactory>
{
    private static readonly Uri Prontidao = new("/health/ready", UriKind.Relative);

    private readonly PortalProntidaoApiFactory _factory;

    public ProntidaoDaPortalTests(PortalProntidaoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "Provedor de identidade fora do ar reprova a prontidão")]
    public async Task Prontidao_ProvedorDeIdentidadeForaDoAr_Reprova()
    {
        _factory.ProvedorDeIdentidade.Status = HttpStatusCode.ServiceUnavailable;
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage resposta = await client.GetAsync(Prontidao);

        resposta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact(DisplayName = "Banco, cache e armazenamento fora do ar não reprovam a prontidão")]
    public async Task Prontidao_DependenciasQueNenhumaRotaUsaForaDoAr_ContinuaApta()
    {
        _factory.ProvedorDeIdentidade.Status = HttpStatusCode.OK;
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage resposta = await client.GetAsync(Prontidao);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Inventário da sonda: uma verificação nova só entra quando uma rota servida passar a
    /// depender dela — inclusive de origem consultada por rede, cuja indisponibilidade aparece na
    /// resposta do endpoint, e não aqui.
    /// </summary>
    [Fact(DisplayName = "A sonda contém só a verificação do provedor de identidade")]
    public void Prontidao_Inventario_SoProvedorDeIdentidade()
    {
        HealthCheckServiceOptions opcoes = _factory.Services
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        opcoes.Registrations
            .Where(r => r.Tags.Contains(HealthChecksServiceCollectionExtensions.ReadyTag))
            .Select(r => r.Name)
            .Should().BeEquivalentTo(["oidc-discovery"]);
    }
}
