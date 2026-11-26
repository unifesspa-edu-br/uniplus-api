namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

/// <summary>
/// As leituras de valor do documento que a forma já aprovou, comuns a todas as partes: o esquema garante o tipo de cada valor
/// presente, então aqui só se converte; o que falta ou não cabe no tipo fica ausente, e quem o confere é o domínio.
/// </summary>
internal static class LeituraDoJsonDoDocumento
{
    internal static JsonObject? Objeto(JsonObject? pai, string propriedade) => pai?[propriedade] as JsonObject;

    internal static string? Texto(JsonObject? pai, string propriedade) =>
        pai?[propriedade] is JsonValue valor ? valor.GetValue<string>() : null;

    internal static string[] Textos(JsonObject pai, string propriedade) =>
        pai[propriedade] is JsonArray lista ? [.. lista.Select(static texto => texto!.GetValue<string>())] : [];

    // O valor que o tipo não comporta (número fora do inteiro de 32 bits, data mal escrita) fica ausente, e o domínio o recusa.
    internal static T? Valor<T>(JsonObject? pai, string propriedade)
        where T : struct =>
        pai?[propriedade] is JsonValue valor && valor.TryGetValue(out T convertido) ? convertido : null;

    // O esquema só deixa passar um dos códigos do catálogo; o que o domínio não conhece fica ausente, e o domínio o recusa.
    internal static T? CodigoDoCatalogo<T>(string? codigo)
        where T : struct, Enum =>
        CatalogoDe<T>.TentarObter(codigo, out T valor) ? valor : null;

    internal static IEnumerable<(int Posicao, JsonObject Item)> Itens(JsonObject? pai, string propriedade) =>
        pai?[propriedade] is JsonArray lista ? lista.Select(static (item, posicao) => (posicao, item!.AsObject())) : [];

    internal static ReferenciaDeRegraNoDocumento? Referencia(JsonObject? referencia) =>
        referencia is null ? null : new(Texto(referencia, "codigo"), Texto(referencia, "versao"));
}
