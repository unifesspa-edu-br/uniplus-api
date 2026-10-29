using Unifesspa.UniPlus.Infrastructure.Core.Routing;

[assembly: ApiModule(
    global::Unifesspa.UniPlus.Autorizacao.API.AutorizacaoApiAssemblyMarker.ModuleName)]

namespace Unifesspa.UniPlus.Autorizacao.API;

/// <summary>
/// Marker usado por carregadores de assembly (ArchUnitNET, fixtures de testes) para obter
/// referência inequívoca ao assembly Autorização.API.
/// </summary>
public sealed class AutorizacaoApiAssemblyMarker
{
    public const string ModuleName = "autorizacao";

    private AutorizacaoApiAssemblyMarker()
    {
    }
}
