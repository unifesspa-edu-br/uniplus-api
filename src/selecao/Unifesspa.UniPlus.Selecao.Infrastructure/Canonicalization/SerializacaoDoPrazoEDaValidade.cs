namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Globalization;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Escrita do prazo de emissão e da validade de uma exigência no envelope, pelo código que cada enum declara.
/// Todo detalhe que não se aplica à escolha é escrito como <c>null</c>.
/// </summary>
internal static class SerializacaoDoPrazoEDaValidade
{
    internal static JsonObject? Prazo(PrazoDeEmissao? prazo) => prazo is null ? null : new()
    {
        ["escolha"] = prazo.Escolha.Codigo(),
        ["valor"] = prazo.Valor,
        ["unidade"] = prazo.Unidade?.Codigo(),
        ["referencia"] = Referencia(prazo.Referencia),
        ["efeitoForaDoPrazo"] = prazo.EfeitoForaDoPrazo?.Codigo(),
    };

    internal static JsonObject? Validade(ValidadeDoDocumento? validade) => validade is null ? null : new()
    {
        ["escolha"] = validade.Escolha.Codigo(),
        ["referencia"] = Referencia(validade.Referencia),
        ["efeitoDoVencimento"] = validade.EfeitoDoVencimento?.Codigo(),
    };

    private static JsonObject? Referencia(DataDeReferenciaDoDocumento? referencia) => referencia is null ? null : new()
    {
        ["tipo"] = referencia.Tipo.Codigo(),
        ["data"] = referencia.Data?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["faseId"] = referencia.FaseId,
    };
}
