namespace Unifesspa.UniPlus.Host;

using Microsoft.AspNetCore.Mvc.ApplicationModels;

/// <summary>
/// Convention do composition root do monólito modular: atribui a cada
/// controller o <c>ApiExplorer.GroupName</c> do seu módulo, derivado do
/// namespace. Sem isso, o Microsoft.AspNetCore.OpenApi inclui todo endpoint com
/// <c>GroupName == null</c> em TODOS os documentos — no processo único, cada
/// <c>/openapi/{modulo}.json</c> listaria os endpoints dos 4 módulos.
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
/// <c>contracts/openapi.*.json</c> ficam inalterados. O mapa namespace→documento
/// trata o caso <c>OrganizacaoInstitucional</c> → <c>organizacao</c> (o nome do
/// documento não é o nome do namespace).</para>
/// </remarks>
internal sealed class ModuleApiGroupingConvention : IApplicationModelConvention
{
    // (prefixo de namespace do módulo, nome do documento OpenAPI). O nome do
    // documento casa o passado a AddUniPlusOpenApi("<doc>", ...) em cada módulo.
    private static readonly (string NamespacePrefix, string GroupName)[] Mapa =
    [
        ("Unifesspa.UniPlus.Configuracao.", "configuracao"),
        ("Unifesspa.UniPlus.OrganizacaoInstitucional.", "organizacao"),
        ("Unifesspa.UniPlus.Selecao.", "selecao"),
        ("Unifesspa.UniPlus.Ingresso.", "ingresso"),
    ];

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

            string? ns = controller.ControllerType.Namespace;
            if (ns is null)
            {
                continue;
            }

            foreach ((string prefixo, string grupo) in Mapa)
            {
                if (ns.StartsWith(prefixo, StringComparison.Ordinal))
                {
                    controller.ApiExplorer.GroupName = grupo;
                    break;
                }
            }
        }
    }
}
