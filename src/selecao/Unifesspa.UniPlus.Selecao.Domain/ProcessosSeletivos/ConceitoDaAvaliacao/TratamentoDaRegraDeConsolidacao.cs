namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;

using System.Text.Json;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O tratamento que a versão da regra de consolidação declara no <c>esquema_args</c> para quando o critério não
/// converge (ADR-0150, emenda 3). Ponto único da leitura da chave, usado por quem lê a declaração do conceito
/// categórico e por quem conta os corretores em campo.
/// </summary>
internal static class TratamentoDaRegraDeConsolidacao
{
    internal const string ChaveDoTratamento = "quando_nao_consolida";
    private const string ChaveDoCriterio = "criterio";

    /// <summary>
    /// Falso quando a chave está presente e não é um token conhecido (esquema inválido). Verdadeiro com
    /// <paramref name="tratamento"/> nulo quando a versão não declara a chave nem critério, como as regras numéricas: a
    /// versão que declara critério sem tratamento está incompleta, e a leitura a recusa.
    /// </summary>
    internal static bool Tentar(RegraCatalogo regra, out TratamentoDaFaltaDeConsolidacao? tratamento)
    {
        tratamento = null;
        if (regra.EsquemaArgs.ValueKind != JsonValueKind.Object || !regra.EsquemaArgs.TryGetProperty(ChaveDoTratamento, out JsonElement declarado))
        {
            return regra.EsquemaArgs.ValueKind == JsonValueKind.Object && !regra.EsquemaArgs.TryGetProperty(ChaveDoCriterio, out _);
        }

        string? token = declarado.ValueKind == JsonValueKind.String ? declarado.GetString() : null;
        bool conhecido = TratamentoDaFaltaDeConsolidacaoCodigo.TentarConverter(token, out TratamentoDaFaltaDeConsolidacao lido);
        tratamento = conhecido ? lido : null;

        return conhecido;
    }
}
