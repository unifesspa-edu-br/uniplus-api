namespace Unifesspa.UniPlus.Parametrizacao.API;

/// <summary>
/// Marker type usado por carregadores de assembly (ArchUnitNET, fixtures).
/// Necessário porque o entry point top-level (<c>Program</c>) compartilha
/// nome com os outros 4 módulos.
/// </summary>
public sealed class ParametrizacaoApiAssemblyMarker
{
    private ParametrizacaoApiAssemblyMarker()
    {
    }
}
