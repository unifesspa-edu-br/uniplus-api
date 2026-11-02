namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Text.Json;

/// <summary>
/// O valor de uma resposta de município nas três formas que ele toma: o código IBGE como texto, que é o que o
/// navegador envia e o rascunho guarda; e <c>{ codigo, rotulo }</c>, que a inscrição enviada guarda, com o
/// nome que o Geo conferiu no envio. Todo leitor que decide por regra lê só o código, por <see cref="Codigo"/>.
/// </summary>
public static class ValorDeMunicipio
{
    private const string PropriedadeCodigo = "codigo";
    private const string PropriedadeRotulo = "rotulo";

    /// <summary>O valor gravado na inscrição enviada: o código e o nome que o Geo conferiu para ele.</summary>
    public static JsonElement Gravado(string codigo, string rotulo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(rotulo);

        return JsonSerializer.SerializeToElement(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PropriedadeCodigo] = codigo,
            [PropriedadeRotulo] = rotulo,
        });
    }

    /// <summary>
    /// Lê o valor em uma das duas formas válidas: o código como texto, sem rótulo, ou o objeto com o código e o rótulo,
    /// ambos preenchidos e sem nenhuma outra propriedade. Qualquer outra forma é recusada.
    /// </summary>
    public static bool TentarLer(JsonElement valor, out string codigo, out string? rotulo)
    {
        codigo = string.Empty;
        rotulo = null;

        if (valor.ValueKind == JsonValueKind.String)
        {
            codigo = valor.GetString()!;
            return !string.IsNullOrWhiteSpace(codigo);
        }

        if (valor.ValueKind != JsonValueKind.Object || valor.EnumerateObject().Count() != 2)
        {
            return false;
        }

        if (!valor.TryGetProperty(PropriedadeCodigo, out JsonElement codigoLido) || codigoLido.ValueKind != JsonValueKind.String
            || !valor.TryGetProperty(PropriedadeRotulo, out JsonElement rotuloLido) || rotuloLido.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        codigo = codigoLido.GetString()!;
        rotulo = rotuloLido.GetString()!;
        return !string.IsNullOrWhiteSpace(codigo) && !string.IsNullOrWhiteSpace(rotulo);
    }

    /// <summary>O código IBGE do valor, seja ele o texto ou o objeto gravado; nulo quando o valor não é de município.</summary>
    public static string? Codigo(JsonElement valor) => TentarLer(valor, out string codigo, out _) ? codigo : null;

    /// <summary>
    /// O valor reduzido ao código, como as regras o leem. O que não tem forma de município fica como está, para
    /// que a regra o trate como trata qualquer resposta de forma inesperada.
    /// </summary>
    public static JsonElement ComoCodigo(JsonElement valor) =>
        valor.ValueKind == JsonValueKind.Object && Codigo(valor) is { } codigo ? JsonSerializer.SerializeToElement(codigo) : valor;
}
