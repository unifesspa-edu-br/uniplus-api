using Unifesspa.UniPlus.Infrastructure.Core.Routing;

[assembly: ApiModule(
    global::Unifesspa.UniPlus.Configuracao.API.ConfiguracaoApiAssemblyMarker.ModuleName)]

namespace Unifesspa.UniPlus.Configuracao.API;

/// <summary>
/// Marker type usado por carregadores de assembly (ArchUnitNET, fixtures).
/// Necessário porque o entry point top-level (<c>Program</c>) compartilha
/// nome com os outros módulos.
/// </summary>
public sealed class ConfiguracaoApiAssemblyMarker
{
    public const string ModuleName = "configuracao";

    private ConfiguracaoApiAssemblyMarker()
    {
    }
}
