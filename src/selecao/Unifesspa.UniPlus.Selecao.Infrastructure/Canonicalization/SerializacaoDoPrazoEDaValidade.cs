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
    internal static JsonObject? Prazo(PrazoDeEmissao? prazo)
    {
        if (prazo is null)
            return null;
        JsonObject objeto = new()
        {
            ["escolha"] = prazo.Escolha.Codigo(),
            ["valor"] = prazo.Valor,
            ["unidade"] = prazo.Unidade?.Codigo(),
            ["referencia"] = Referencia(prazo.Referencia),
            ["efeitoForaDoPrazo"] = prazo.EfeitoForaDoPrazo?.Codigo(),
        };
        ContextoDosRotulos.Acrescentar(objeto, "escolha", prazo.Escolha);
        ContextoDosRotulos.Acrescentar(objeto, "unidade", prazo.Unidade);
        ContextoDosRotulos.Acrescentar(objeto, "efeitoForaDoPrazo", prazo.EfeitoForaDoPrazo);
        return objeto;
    }

    internal static JsonObject? Validade(ValidadeDoDocumento? validade)
    {
        if (validade is null)
            return null;
        JsonObject objeto = new()
        {
            ["escolha"] = validade.Escolha.Codigo(),
            ["referencia"] = Referencia(validade.Referencia),
            ["efeitoDoVencimento"] = validade.EfeitoDoVencimento?.Codigo(),
        };
        ContextoDosRotulos.Acrescentar(objeto, "escolha", validade.Escolha);
        ContextoDosRotulos.Acrescentar(objeto, "efeitoDoVencimento", validade.EfeitoDoVencimento);
        return objeto;
    }

    private static JsonObject? Referencia(DataDeReferenciaDoDocumento? referencia)
    {
        if (referencia is null)
            return null;
        JsonObject objeto = new()
        {
            ["tipo"] = referencia.Tipo.Codigo(),
            ["data"] = referencia.Data?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["faseId"] = referencia.FaseId,
        };
        ContextoDosRotulos.Acrescentar(objeto, "tipo", referencia.Tipo);
        return objeto;
    }
}
