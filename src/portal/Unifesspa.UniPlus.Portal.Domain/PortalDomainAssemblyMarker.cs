namespace Unifesspa.UniPlus.Portal.Domain;

/// <summary>
/// Marker type usado por carregadores de assembly (ArchUnitNET, fixtures de
/// testes) para obter referência inequívoca ao assembly Portal.Domain.
/// O domínio do Portal ainda está vazio (esqueleto da Story #336); assim
/// que houver entidades públicas, qualquer uma delas pode substituir este
/// marker como âncora typeof().Assembly.
/// </summary>
public sealed class PortalDomainAssemblyMarker
{
    private PortalDomainAssemblyMarker()
    {
    }
}
