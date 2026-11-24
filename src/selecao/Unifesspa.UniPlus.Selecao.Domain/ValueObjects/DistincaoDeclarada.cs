namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Os dados informados para distinguir as apresentações de um documento.</summary>
public sealed record DistincaoDeclarada(
    CriterioDeDistincaoDasApresentacoes? Criterio, DateOnly? DataReferencia, IReadOnlyList<string>? OcorrenciasEsperadas, bool CriterioReconhecido = true)
{
    public static DistincaoDeclarada Interpretar(string? codigo, DateOnly? dataReferencia, IReadOnlyList<string>? ocorrenciasEsperadas)
    {
        bool reconhecido = CatalogoDe<CriterioDeDistincaoDasApresentacoes>.TentarObter(codigo, out CriterioDeDistincaoDasApresentacoes criterio);
        return new(reconhecido ? criterio : null, dataReferencia, ocorrenciasEsperadas, codigo is null || reconhecido);
    }
}
