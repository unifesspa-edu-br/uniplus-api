namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Contratos;

using System.Text.Json.Nodes;

/// <summary>
/// Percorre o esquema publicado do documento da configuração, segmento a segmento, para saber se um ponteiro JSON (RFC 6901) existe
/// nele: as propriedades (inclusive as que vêm de <c>allOf</c>, <c>anyOf</c> e <c>oneOf</c>), o item de cada lista quando o
/// segmento é numérico, seja qual for a posição, e as referências internas e para outro arquivo. O contrato é lido como dado.
/// </summary>
internal sealed class CaminhosDoDocumentoNoEsquema
{
    private const string Documento = "documento.schema.json";
    private static readonly string[] Combinacoes = ["allOf", "anyOf", "oneOf"];

    internal static string DiretorioPublicado { get; } = Path.Join(AppContext.BaseDirectory, "Contratos", "selecao-configuracao", "v1");

    internal static CaminhosDoDocumentoNoEsquema Publicado { get; } = new(DiretorioPublicado);

    private readonly Dictionary<string, JsonNode> _arquivos = new(StringComparer.Ordinal);
    private readonly string _diretorio;

    internal CaminhosDoDocumentoNoEsquema(string diretorio) => _diretorio = diretorio;

    /// <summary>O ponteiro percorrido até o primeiro segmento que o esquema não tem, esse incluído; nulo quando o ponteiro inteiro existe.</summary>
    internal string? PrimeiroSegmentoAusente(string ponteiro)
    {
        List<NoDoEsquema> atuais = [Resolver(new NoDoEsquema(Raiz(Documento), Documento))];
        string percorrido = string.Empty;
        foreach (SegmentoDoCaminho segmento in Segmentos(ponteiro))
        {
            percorrido += "/" + segmento.Texto;
            atuais = [.. atuais.SelectMany(atual => Avancar(atual, segmento))];
            if (atuais.Count == 0)
            {
                return percorrido;
            }
        }

        return null;
    }

    private IEnumerable<NoDoEsquema> Avancar(NoDoEsquema atual, SegmentoDoCaminho segmento) =>
        Fragmentos(atual).Select(fragmento => Filho(fragmento, segmento)).OfType<NoDoEsquema>();

    // O segmento numérico é o item da lista ou o valor do mapa, qualquer que seja a posição.
    private NoDoEsquema? Filho(NoDoEsquema fragmento, SegmentoDoCaminho segmento)
    {
        JsonNode? filho = segmento.Indice
            ? fragmento.Esquema["items"] ?? fragmento.Esquema["additionalProperties"]
            : fragmento.Esquema["properties"]?[segmento.Nome];

        return filho is JsonObject ? Resolver(new NoDoEsquema(filho, fragmento.Arquivo)) : null;
    }

    // O esquema e tudo o que ele combina: as propriedades de um allOf valem para o esquema que o contém.
    private IEnumerable<NoDoEsquema> Fragmentos(NoDoEsquema atual) =>
    [
        atual,
        .. Combinacoes
            .SelectMany(combinacao => atual.Esquema[combinacao] is JsonArray alternativas ? alternativas.AsEnumerable() : [])
            .SelectMany(alternativa => Fragmentos(Resolver(new NoDoEsquema(alternativa!, atual.Arquivo)))),
    ];

    private NoDoEsquema Resolver(NoDoEsquema no)
    {
        NoDoEsquema atual = no;
        while (atual.Esquema["$ref"]?.GetValue<string>() is { } referencia)
        {
            string[] partes = referencia.Split('#', 2);
            string destino = partes[0].Length == 0 ? atual.Arquivo : partes[0];
            atual = new NoDoEsquema(Apontado(Raiz(destino), partes.ElementAtOrDefault(1) ?? string.Empty), destino);
        }

        return atual;
    }

    private static JsonNode Apontado(JsonNode raiz, string ponteiro) =>
        ponteiro.Split('/', StringSplitOptions.RemoveEmptyEntries).Aggregate(raiz, static (esquema, passo) => esquema[passo]!);

    private JsonNode Raiz(string arquivo)
    {
        if (!_arquivos.TryGetValue(arquivo, out JsonNode? raiz))
        {
            raiz = JsonNode.Parse(File.ReadAllText(Path.Join(_diretorio, arquivo)))!;
            _arquivos[arquivo] = raiz;
        }

        return raiz;
    }

    private static IEnumerable<SegmentoDoCaminho> Segmentos(string ponteiro)
    {
        if (!ponteiro.StartsWith('/'))
        {
            throw new ArgumentException($"O ponteiro '{ponteiro}' não começa na raiz do documento.", nameof(ponteiro));
        }

        return ponteiro.Split('/')[1..].Select(static texto => new SegmentoDoCaminho(
            texto.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal),
            texto.Length > 0 && texto.All(char.IsAsciiDigit),
            texto));
    }

    private sealed record NoDoEsquema(JsonNode Esquema, string Arquivo);

    private sealed record SegmentoDoCaminho(string Nome, bool Indice, string Texto);
}
