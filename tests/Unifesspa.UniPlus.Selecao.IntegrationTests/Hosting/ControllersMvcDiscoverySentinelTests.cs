namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Hosting;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using Infrastructure;

/// <summary>
/// Sentinela contra recidiva da regressão #173 — controllers MVC marcados
/// como <c>internal sealed</c> são silenciosamente ignorados pelo
/// <c>ControllerFeatureProvider</c>, deixando o MVC sem rotas para esses
/// controllers e nenhum diagnóstico claro em runtime.
/// </summary>
/// <remarks>
/// O guard usa <see cref="EndpointDataSource"/> resolvido pelo test host —
/// inspeciona os endpoints registrados após pipeline build e asserts que
/// cada controller esperado tem ao menos uma rota mapeada via
/// <see cref="ControllerActionDescriptor"/>. Falha cedo com mensagem
/// orientando a checar o modificador de acesso da classe + supressão de
/// CA1515 quando aplicável.
/// </remarks>
public sealed class ControllersMvcDiscoverySentinelTests : IClassFixture<SelecaoApiFactory>
{
    private readonly SelecaoApiFactory _factory;

    public ControllersMvcDiscoverySentinelTests(SelecaoApiFactory factory) => _factory = factory;

    [Theory(DisplayName = "Controller MVC do módulo expõe ao menos uma rota no test host")]
    [InlineData("EditalController")]
    public void Controller_RegistrouRotas_NoEndpointDataSource(string controllerName)
    {
        // CreateClient força WebApplicationFactory a buildar o test host,
        // o que faz o pipeline MVC descobrir e mapear endpoints.
        using HttpClient _ = _factory.CreateClient();

        EndpointDataSource dataSource = _factory.Services.GetRequiredService<EndpointDataSource>();

        IEnumerable<ControllerActionDescriptor> actions = dataSource.Endpoints
            .Select(static endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>())
            .Where(static descriptor => descriptor is not null)
            .Cast<ControllerActionDescriptor>()
            .Where(descriptor => string.Equals(
                descriptor.ControllerTypeInfo.Name, controllerName, StringComparison.Ordinal));

        actions.Should().NotBeEmpty(
            because: $"{controllerName} deve estar registrado pelo MVC. Recidiva mais provável: o "
            + "controller virou `internal sealed` (ControllerFeatureProvider só descobre `public`). "
            + "Conferir modificador de acesso e supressão de CA1515 com a justificativa "
            + "\"ASP.NET Core ControllerFeatureProvider só descobre controllers public\".");
    }
}
