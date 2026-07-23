namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As regras de bytes do envelope de congelamento — <c>canonical-json/sha256@v1</c>.
/// </summary>
/// <remarks>
/// <para>
/// A forma, tal como é:
/// </para>
/// <list type="number">
///   <item>chaves de todo objeto reordenadas recursivamente por comparação ordinal; arrays
///   preservam a ordem, que é semântica;</item>
///   <item>toda string de negócio normalizada para <b>NFC</b> — a forma pré-composta e a
///   sequência com acento combinante são o mesmo texto, e têm de virar os mesmos bytes;</item>
///   <item>todo número é <b>inteiro de 64 bits</b> na forma canônica (<c>-0</c> vira <c>0</c>,
///   sem zero à esquerda, sem expoente): o léxico com que o valor entrou não sobrevive. Um
///   número fracionário ou fora do alcance de <see cref="long"/> é <b>recusado</b>, não
///   aproximado;</item>
///   <item><c>null</c> explícito é <b>preservado</b> — o envelope distingue "campo ausente" de
///   "campo presente e vazio";</item>
///   <item>escape mínimo: aspa e contrabarra e os cinco controles com forma curta
///   (<c>\" \\ \b \t \n \f \r</c>), os demais controles como <c>\u00xx</c>, e <b>todo o resto
///   literal</b> — inclusive acentuação e os caracteres que só seriam perigosos dentro de HTML
///   (o envelope nunca é interpolado em HTML). É o que o encoder relaxado do runtime emite;</item>
///   <item>digest SHA-256, hex minúsculo, sobre os bytes assim produzidos.</item>
/// </list>
/// <para>
/// Enquanto não há produção, esta forma <b>evolui livremente</b>: mudar uma regra reescreve a
/// fixture golden, não gera um perfil congelado ao lado. A imutabilidade "um perfil não muda
/// depois de emitir" passa a valer na primeira emissão de <b>produção</b> (a versão
/// <c>1.0.0</c>). Este perfil serializa por conta própria — não passa por
/// <see cref="HashCanonicalComputer.ComputeSnapshotBytes"/>, que tem forma distinta e é
/// compartilhado com o histórico de <c>ObrigatoriedadeLegal</c>, fora do envelope.
/// </para>
/// </remarks>
public sealed class PerfilCanonicoV1 : IPerfilCanonico
{
    private static readonly JsonWriterOptions OpcoesEscrita = new()
    {
        Indented = false,
        // Escape mínimo, tudo o mais literal — inclusive não-ASCII e os sensíveis a HTML.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Instância única — o perfil não tem estado.</summary>
    public static readonly PerfilCanonicoV1 Instancia = new();

    private PerfilCanonicoV1()
    {
    }

    public string Algoritmo => "canonical-json/sha256@v1";

    public byte[] Serializar(JsonObject payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        JsonNode canonico = Canonicalizar(payload);

        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer, OpcoesEscrita))
        {
            canonico.WriteTo(writer);
        }

        return buffer.ToArray();
    }

    public string HashHex(byte[] bytes) => HashCanonicalComputer.ComputeSha256Hex(bytes);

    /// <summary>
    /// Reescreve a árvore na forma canônica: objetos com chaves ordenadas, strings em NFC,
    /// números como inteiro canônico, <c>null</c> e ordem de array preservados.
    /// </summary>
    private static JsonNode Canonicalizar(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                JsonObject ordenado = [];
                foreach (KeyValuePair<string, JsonNode?> par in obj.OrderBy(static p => p.Key, StringComparer.Ordinal))
                {
                    ordenado[par.Key] = par.Value is null ? null : Canonicalizar(par.Value);
                }
                return ordenado;

            case JsonArray arr:
                JsonArray reescrito = [];
                foreach (JsonNode? item in arr)
                {
                    reescrito.Add(item is null ? null : Canonicalizar(item));
                }
                return reescrito;

            case JsonValue valor:
                return CanonicalizarValor(valor);

            default:
                // JsonNode só tem estas três formas concretas; o default é inalcançável.
                return node.DeepClone();
        }
    }

    private static JsonNode CanonicalizarValor(JsonValue valor)
    {
        switch (valor.GetValueKind())
        {
            case JsonValueKind.String when valor.TryGetValue(out string? texto):
                // NFC de toda string de negócio — fecha o furo do valor copiado por texto cru
                // (ex.: valor de condição DNF) que não passou por normalização na projeção.
                return JsonValue.Create(HashCanonicalComputer.NormalizeNfc(texto));

            case JsonValueKind.String:
                // String-kind com backing não-textual (Guid, data): já é ASCII canônico, sem
                // sequência combinante a normalizar.
                return valor.DeepClone();

            case JsonValueKind.Number:
                return JsonValue.Create(ExigirInteiro(valor));

            default:
                // true/false não têm léxico ambíguo; reusar é seguro (o nó vem de um clone da árvore).
                return valor.DeepClone();
        }
    }

    /// <summary>
    /// O número canônico é o inteiro de 64 bits. Cobre os dois backings de <see cref="JsonValue"/>:
    /// o parseado (respeita a forma textual — <c>1.0</c>/<c>1e2</c> não são inteiros) e o
    /// criado a partir de um número CLR (<c>Create(int)</c> não satisfaz <c>TryGetValue&lt;long&gt;</c>).
    /// </summary>
    private static long ExigirInteiro(JsonValue valor)
    {
        if (valor.TryGetValue(out JsonElement elemento) && elemento.ValueKind == JsonValueKind.Number)
        {
            if (elemento.TryGetInt64(out long lidoDoElemento))
            {
                return lidoDoElemento;
            }

            throw new PayloadForaDoPerfilCanonicoException(
                $"O número '{elemento.GetRawText()}' não é um inteiro de 64 bits — a forma canônica não representa " +
                "fração, expoente nem valor fora do alcance de Int64.");
        }

        if (valor.TryGetValue(out long comoLong))
        {
            return comoLong;
        }

        if (valor.TryGetValue(out int comoInt))
        {
            return comoInt;
        }

        throw new PayloadForaDoPerfilCanonicoException(
            "Um número do payload não é um inteiro de 64 bits — a forma canônica não representa fração, " +
            "expoente nem valor fora do alcance de Int64.");
    }
}
