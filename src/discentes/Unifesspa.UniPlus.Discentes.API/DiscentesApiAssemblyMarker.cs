using Unifesspa.UniPlus.Infrastructure.Core.Routing;

[assembly: ApiModule(
    global::Unifesspa.UniPlus.Discentes.API.DiscentesApiAssemblyMarker.ModuleName)]

namespace Unifesspa.UniPlus.Discentes.API;

/// <summary>
/// Marker type usado por carregadores de assembly e dono da identidade pública
/// canônica do módulo Discentes.
/// </summary>
public sealed class DiscentesApiAssemblyMarker
{
    public const string ModuleName = "discentes";

    private DiscentesApiAssemblyMarker()
    {
    }
}
