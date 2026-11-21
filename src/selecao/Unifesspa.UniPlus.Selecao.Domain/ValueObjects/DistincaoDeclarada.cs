namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Os dados informados para distinguir as apresentações de um documento.</summary>
public sealed record DistincaoDeclarada(
    ChaveDistincao? Chave, DateOnly? DataReferencia, IReadOnlyList<string>? OcorrenciasEsperadas, bool ChaveReconhecida = true)
{
    public static DistincaoDeclarada Interpretar(string? codigo, DateOnly? dataReferencia, IReadOnlyList<string>? ocorrenciasEsperadas)
    {
        ChaveDistincao? chave = codigo is null ? null : ChaveDistincaoCodigo.FromCodigo(codigo);
        return new(chave, dataReferencia, ocorrenciasEsperadas, codigo is null || chave != ChaveDistincao.Nenhuma);
    }
}
