namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Mappings;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using DTOs;

/// <summary>
/// Projeções entre o agregado <see cref="AreaOrganizacional"/> e suas
/// representações públicas — <see cref="AreaOrganizacionalDto"/> para HTTP
/// e <see cref="AreaOrganizacionalView"/> para consumo cross-módulo via
/// <see cref="IAreaOrganizacionalReader"/>.
/// </summary>
public static class AreaOrganizacionalMapping
{
    public static AreaOrganizacionalDto ToDto(this AreaOrganizacional area)
    {
        ArgumentNullException.ThrowIfNull(area);
        return new AreaOrganizacionalDto(
            area.Id,
            area.Codigo.ToString(),
            area.Nome,
            area.Tipo.ToString(),
            area.Descricao,
            area.AdrReferenceCode,
            area.CreatedAt);
    }

    public static AreaOrganizacionalView ToView(this AreaOrganizacional area)
    {
        ArgumentNullException.ThrowIfNull(area);
        return new AreaOrganizacionalView(
            area.Id,
            area.Codigo,
            area.Nome,
            area.Tipo.ToString(),
            area.Descricao,
            area.AdrReferenceCode,
            area.CreatedAt);
    }
}
