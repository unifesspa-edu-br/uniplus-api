namespace Unifesspa.UniPlus.Infrastructure.Core.Routing;

using System.Reflection;

/// <summary>
/// Resolve o módulo de um controller pelo metadado declarado em seu assembly,
/// sem depender do namespace ou do agrupamento do ApiExplorer.
/// </summary>
public static class ApiModuleMetadata
{
    public static string GetRequiredName(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        ApiModuleAttribute? metadata = assembly.GetCustomAttribute<ApiModuleAttribute>();
        return metadata?.Name
            ?? throw new InvalidOperationException(
                $"O assembly '{assembly.GetName().Name}' contém controllers, "
                + $"mas não declara [{nameof(ApiModuleAttribute)}].");
    }
}
