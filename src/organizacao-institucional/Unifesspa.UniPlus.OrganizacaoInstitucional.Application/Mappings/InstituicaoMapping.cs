namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Mappings;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

public static class InstituicaoMapping
{
    public static InstituicaoDto ToDto(this Instituicao instituicao)
    {
        ArgumentNullException.ThrowIfNull(instituicao);
        return new InstituicaoDto(
            instituicao.Id,
            instituicao.CodigoEmec,
            instituicao.Nome,
            instituicao.Sigla,
            instituicao.OrganizacaoAcademica,
            instituicao.CategoriaAdministrativa,
            instituicao.Cnpj,
            instituicao.Mantenedora,
            instituicao.CodigoMantenedoraEmec,
            instituicao.Situacao,
            instituicao.AtoCredenciamento,
            instituicao.AtoRecredenciamento,
            instituicao.ConceitoInstitucional,
            instituicao.Igc,
            instituicao.Website,
            instituicao.EnderecoSede,
            instituicao.MunicipioSede,
            instituicao.UnidadeRaizId,
            instituicao.CreatedAt);
    }

    public static InstituicaoView ToView(this Instituicao instituicao)
    {
        ArgumentNullException.ThrowIfNull(instituicao);
        return new InstituicaoView(
            instituicao.Id,
            instituicao.CodigoEmec,
            instituicao.Nome,
            instituicao.Sigla,
            instituicao.Cnpj,
            instituicao.OrganizacaoAcademica,
            instituicao.CategoriaAdministrativa,
            instituicao.MunicipioSede,
            instituicao.UnidadeRaizId);
    }
}
