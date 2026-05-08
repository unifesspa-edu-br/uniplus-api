namespace Unifesspa.UniPlus.Portal.API;

/// <summary>
/// Marker type usado por carregadores de assembly (ArchUnitNET, fixtures de
/// testes) para obter referência inequívoca ao assembly Portal.API.
/// Necessário porque o entry point top-level (<c>Program</c>) compartilha
/// nome com o entry point de outras APIs do mesmo solution e não permite
/// <c>typeof(Program)</c> sem ambiguidade.
/// </summary>
public sealed class PortalApiAssemblyMarker
{
    private PortalApiAssemblyMarker()
    {
    }
}
