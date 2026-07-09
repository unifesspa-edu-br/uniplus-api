namespace Unifesspa.UniPlus.Publicacoes.Domain;

/// <summary>
/// Marker type usado por carregadores de assembly (ArchUnitNET, fixtures de
/// testes) para obter referência inequívoca ao assembly Publicacoes.Domain.
/// Necessário enquanto o domínio não tem entidades: sem um tipo público
/// âncora, o assembly não é carregável por reflexão.
/// </summary>
public sealed class PublicacoesDomainAssemblyMarker
{
    private PublicacoesDomainAssemblyMarker()
    {
    }
}
