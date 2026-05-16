namespace Unifesspa.UniPlus.Selecao.Application.Mappings;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Mapeamento <c>ObrigatoriedadeLegal</c> → <c>ObrigatoriedadeLegalDto</c>.
/// <c>AreasDeInteresse</c> chega via batch lookup do repositório
/// (<c>ObterAreasVigentesPorIdsAsync</c>) — a entity em si não carrega
/// nav property para a junction (ADR-0060).
/// </summary>
public static class ObrigatoriedadeLegalMapping
{
    public static ObrigatoriedadeLegalDto ToDto(
        ObrigatoriedadeLegal regra,
        IReadOnlySet<AreaCodigo> areasVigentes)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentNullException.ThrowIfNull(areasVigentes);

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
            Proprietario: regra.Proprietario?.Value,
            AreasDeInteresse: [.. areasVigentes
                .Select(a => a.Value)
                .OrderBy(v => v, StringComparer.Ordinal)],
            IsDeleted: regra.IsDeleted);
    }
}
