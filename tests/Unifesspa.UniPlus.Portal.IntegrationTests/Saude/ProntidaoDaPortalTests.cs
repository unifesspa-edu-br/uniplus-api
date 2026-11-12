namespace Unifesspa.UniPlus.Portal.IntegrationTests.Saude;

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.HealthChecks;

/// <summary>
/// A prontidão da Portal responde por quem ela serve (ADR-0131): entra o que alguma rota servida
/// precisa para responder. O provedor de identidade só afeta as rotas autenticadas, que já
/// recusam a requisição sozinhas; retirá-lo da sonda mantém no ar as rotas públicas, e o estado
/// dele segue visível no health completo.
/// </summary>
[Trait("Category", "Integration")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IClassFixture<T> exige tipo de teste público.")]
public sealed class ProntidaoDaPortalTests : IClassFixture<PortalProntidaoApiFactory>
{
    private const string VerificacaoDoProvedor = "oidc-discovery";

    private static readonly Uri Prontidao = new("/health/ready", UriKind.Relative);

    private readonly PortalProntidaoApiFactory _factory;

    public ProntidaoDaPortalTests(PortalProntidaoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "Provedor de identidade fora do ar não reprova a prontidão")]
    public async Task Prontidao_ProvedorDeIdentidadeForaDoAr_ContinuaApta()
    {
        _factory.ProvedorDeIdentidade.Status = HttpStatusCode.ServiceUnavailable;
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage resposta = await client.GetAsync(Prontidao);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "Provedor de identidade fora do ar aparece como degradado no health completo")]
    public async Task HealthCompleto_ProvedorDeIdentidadeForaDoAr_ReportaDegradado()
    {
        _factory.ProvedorDeIdentidade.Status = HttpStatusCode.ServiceUnavailable;
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage resposta = await client.GetAsync(new Uri("/health", UriKind.Relative));

        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("verificacoes").GetProperty(VerificacaoDoProvedor).GetProperty("status")
            .GetString().Should().Be("Degraded");
    }

    [Fact(DisplayName = "Provedor de identidade sem resposta degrada o health completo dentro do limite de espera")]
    public async Task HealthCompleto_ProvedorDeIdentidadeSemResposta_DegradaDentroDoLimite()
    {
        _factory.ProvedorDeIdentidade.Pendurado = true;
        try
        {
            HealthCheckService servico = _factory.Services.GetRequiredService<HealthCheckService>();
            Stopwatch cronometro = Stopwatch.StartNew();

            HealthReport relatorio = await servico.CheckHealthAsync(r => r.Name == VerificacaoDoProvedor);

            relatorio.Entries[VerificacaoDoProvedor].Status.Should().Be(HealthStatus.Degraded);
            cronometro.Elapsed.Should().BeLessThan(OidcDiscoveryHealthCheck.LimiteDeEspera + TimeSpan.FromSeconds(2));
        }
        finally
        {
            _factory.ProvedorDeIdentidade.Pendurado = false;
        }
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
    [Fact(DisplayName = "A sonda não contém verificação alguma")]
    public void Prontidao_Inventario_Vazio()
    {
        HealthCheckServiceOptions opcoes = _factory.Services
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        opcoes.Registrations
            .Where(r => r.Tags.Contains(HealthChecksServiceCollectionExtensions.ReadyTag))
            .Select(r => r.Name)
            .Should().BeEmpty();
    }
}
