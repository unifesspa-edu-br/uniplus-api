namespace Unifesspa.UniPlus.Selecao.Application.Mappings;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Mapeamento <c>ObrigatoriedadeLegal</c> → <c>ObrigatoriedadeLegalDto</c>.
/// A regra é cross-cutting por tipo de processo — sem proprietário nem áreas
/// de interesse.
/// </summary>
public static class ObrigatoriedadeLegalMapping
{
    public static ObrigatoriedadeLegalDto ToDto(ObrigatoriedadeLegal regra)
    {
        ArgumentNullException.ThrowIfNull(regra);

        return new ObrigatoriedadeLegalDto(
            Id: regra.Id,
            TipoEditalCodigo: regra.TipoEditalCodigo,
            Categoria: regra.Categoria,
            RegraCodigo: regra.RegraCodigo,
            Predicado: regra.Predicado,
            DescricaoHumana: regra.DescricaoHumana,
            BaseLegal: regra.BaseLegal,
            AtoNormativoUrl: regra.AtoNormativoUrl,
            PortariaInternaCodigo: regra.PortariaInternaCodigo,
            VigenciaInicio: regra.VigenciaInicio,
            VigenciaFim: regra.VigenciaFim,
            Hash: regra.Hash,
            IsDeleted: regra.IsDeleted);
    }
}
