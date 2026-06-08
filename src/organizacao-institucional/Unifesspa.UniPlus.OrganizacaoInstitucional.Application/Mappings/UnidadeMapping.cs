namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Mappings;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

public static class UnidadeMapping
{
    public static UnidadeDto ToDto(this Unidade unidade)
    {
        ArgumentNullException.ThrowIfNull(unidade);
        return new UnidadeDto(
            unidade.Id,
            unidade.Nome,
            unidade.Alias,
            unidade.Slug.Valor,
            unidade.Sigla,
            unidade.Codigo,
            unidade.UnidadeSuperiorId,
            unidade.Tipo.ToString(),
            unidade.UnidadeAcademica,
            unidade.VigenciaInicio,
            unidade.VigenciaFim,
            unidade.Origem.ToString(),
            unidade.CreatedAt);
    }

    public static UnidadeView ToView(this Unidade unidade)
    {
        ArgumentNullException.ThrowIfNull(unidade);
        return new UnidadeView(
            unidade.Id,
            unidade.Sigla,
            unidade.Slug.Valor,
            unidade.Nome,
            unidade.Alias,
            unidade.Tipo.ToString(),
            unidade.UnidadeAcademica,
            unidade.UnidadeSuperiorId);
    }
}
