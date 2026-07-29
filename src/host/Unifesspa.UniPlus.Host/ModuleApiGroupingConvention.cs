namespace Unifesspa.UniPlus.Host;

using Microsoft.AspNetCore.Mvc.ApplicationModels;

using Unifesspa.UniPlus.Infrastructure.Core.Routing;

/// <summary>
/// Convention do composition root do monólito modular: atribui a cada
/// controller o <c>ApiExplorer.GroupName</c> declarado pelo assembly do módulo.
/// Sem isso, o Microsoft.AspNetCore.OpenApi inclui todo endpoint com
/// <c>GroupName == null</c> em TODOS os documentos — no processo único, cada
/// <c>/openapi/{modulo}.json</c> listaria os endpoints dos 5 módulos.
/// </summary>
/// <remarks>
/// <para>Com o <c>GroupName</c> atribuído, o <c>ShouldInclude</c> default do
/// <c>AddOpenApi(documentName)</c> (<c>GroupName == null || GroupName ==
/// documentName</c>) isola cada documento ao seu módulo. Endpoints compartilhados
/// (auth, profile, smoke) permanecem com <c>GroupName == null</c> e seguem
/// aparecendo em todos os documentos — espelhando o comportamento standalone.</para>
///
/// <para>Vive apenas no host: os módulos standalone têm um único documento, sem
/// possibilidade de vazamento, então não recebem a convention e seus baselines
/// <c>contracts/openapi.*.json</c> ficam inalterados. O metadado por assembly
/// também evita derivar o contrato público de nomes de namespace.</para>
/// </remarks>
internal sealed class ModuleApiGroupingConvention : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        ArgumentNullException.ThrowIfNull(application);

        foreach (ControllerModel controller in application.Controllers)
        {
            // Respeita override explícito ([ApiExplorerSettings(GroupName=...)]).
            if (controller.ApiExplorer.GroupName is not null)
            {
                continue;
            }

            controller.ApiExplorer.GroupName = ApiModuleMetadata.GetRequiredName(
                controller.ControllerType.Assembly);
        }
    }
}
