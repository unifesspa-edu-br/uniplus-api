using Unifesspa.UniPlus.Infrastructure.Core.Routing;

[assembly: ApiModule(
    global::Unifesspa.UniPlus.OrganizacaoInstitucional.API.OrganizacaoApiAssemblyMarker.ModuleName)]

namespace Unifesspa.UniPlus.OrganizacaoInstitucional.API;

/// <summary>
/// Marker type usado por carregadores de assembly (ArchUnitNET, fixtures de
/// testes) para obter referência inequívoca ao assembly
/// OrganizacaoInstitucional.API. Necessário porque o entry point top-level
/// (<c>Program</c>) compartilha nome com os entry points dos outros 3 módulos
/// — um marker dedicado evita ambiguidade no carregador de assemblies.
/// </summary>
public sealed class OrganizacaoApiAssemblyMarker
{
    public const string ModuleName = "organizacao";

    private OrganizacaoApiAssemblyMarker()
    {
    }
}
