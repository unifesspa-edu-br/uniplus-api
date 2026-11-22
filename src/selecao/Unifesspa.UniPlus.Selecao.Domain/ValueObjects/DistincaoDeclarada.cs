namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Os dados informados para distinguir as apresentações de um documento.</summary>
public sealed record DistincaoDeclarada(
    CriterioDeDistincaoDasApresentacoes? Criterio, DateOnly? DataReferencia, IReadOnlyList<string>? OcorrenciasEsperadas, bool CriterioReconhecido = true)
{
    public static DistincaoDeclarada Interpretar(string? codigo, DateOnly? dataReferencia, IReadOnlyList<string>? ocorrenciasEsperadas)
    {
        CriterioDeDistincaoDasApresentacoes? criterio = codigo is null ? null : CriterioDeDistincaoDasApresentacoesCodigo.FromCodigo(codigo);
        return new(criterio, dataReferencia, ocorrenciasEsperadas, codigo is null || criterio != CriterioDeDistincaoDasApresentacoes.Nenhuma);
    }
}
