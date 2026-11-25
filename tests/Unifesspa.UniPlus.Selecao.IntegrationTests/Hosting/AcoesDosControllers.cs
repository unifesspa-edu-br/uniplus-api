namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Hosting;

using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;

internal static class AcoesDosControllers
{
    internal static IEnumerable<ControllerActionDescriptor> Obter(EndpointDataSource dataSource) => dataSource.Endpoints
        .Select(static endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>())
        .Where(static descriptor => descriptor is not null)
        .Cast<ControllerActionDescriptor>();
}
