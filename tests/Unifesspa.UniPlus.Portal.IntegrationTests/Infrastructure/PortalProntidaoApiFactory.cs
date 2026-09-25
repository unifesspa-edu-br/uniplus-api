namespace Unifesspa.UniPlus.Portal.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;
using System.Net;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.HealthChecks;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Portal.API;

/// <summary>
/// Host da Portal para os testes de prontidão: banco, cache e armazenamento de objetos ficam
/// configurados com endereços inalcançáveis e nenhuma verificação de infraestrutura é removida,
/// de modo que qualquer uma delas que entre na sonda reprova a prontidão. O provedor de
/// identidade responde pelo <see cref="ProvedorDeIdentidadeSimulado"/>, controlado pelo teste.
/// </summary>
/// <remarks>
/// A mensageria fica de fora da configuração: ligar o endereço dela muda o transporte do
/// Wolverine no arranque, e a ausência da verificação correspondente já é conferida pelo teste
/// que inventaria as verificações da sonda.
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IClassFixture<T> exige fixture pública.")]
public sealed class PortalProntidaoApiFactory : ApiFactoryBase<PortalApiAssemblyMarker>
{
    private const string BancoInalcancavel =
        "Host=integration-not-real;Database=portal;Username=u;Password=p;Timeout=2";

    public ProvedorDeIdentidadeSimulado ProvedorDeIdentidade { get; } = new();

    protected override ISet<string> InfraHealthCheckNamesToRemoveForTests { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    protected override IEnumerable<KeyValuePair<string, string?>> GetConfigurationOverrides() =>
    [
        new("ConnectionStrings:PortalDb", BancoInalcancavel),
        new("Redis:ConnectionString", "integration-not-real:6379,abortConnect=true,connectTimeout=500"),
        new("Storage:Endpoint", "integration-not-real:9000"),
        new("Storage:AccessKey", "chave"),
        new("Storage:SecretKey", "segredo"),
        new("Kafka:BootstrapServers", string.Empty),
        new("Auth:Authority", "http://localhost/test-realm"),
        new("Auth:Audience", "uniplus"),
    ];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
            services.AddHttpClient(nameof(OidcDiscoveryHealthCheck))
                .ConfigurePrimaryHttpMessageHandler(() => new RespostaDoProvedorHandler(ProvedorDeIdentidade)));
    }

    private sealed class RespostaDoProvedorHandler(ProvedorDeIdentidadeSimulado provedor) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(provedor.Status));
    }
}

/// <summary>
/// Estado do provedor de identidade visto pela verificação de descoberta OIDC.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "Exposto pela fixture pública.")]
public sealed class ProvedorDeIdentidadeSimulado
{
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
}
