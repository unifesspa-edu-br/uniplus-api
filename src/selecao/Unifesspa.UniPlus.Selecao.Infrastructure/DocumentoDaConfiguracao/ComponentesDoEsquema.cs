namespace Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

using System.Globalization;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

/// <summary>
/// Os componentes que a especificação OpenAPI publica a partir do esquema do documento de configuração. Parte do arquivo
/// raiz e segue os <c>$ref</c>: vira componente só o que é alcançado (a raiz de cada arquivo citado, inteira, e cada
/// definição citada), e as referências passam a apontar para <c>#/components/schemas</c>. <c>$id</c>, <c>$schema</c> e
/// <c>$defs</c> não entram: um <c>$id</c> dentro do componente trocaria a URI base das referências.
/// </summary>
/// <remarks>
/// A função é pura: o esquema de origem nunca é alterado, e cada geração devolve nós novos.
/// </remarks>
internal sealed class ComponentesDoEsquema
{
    private const string Referencia = "$ref";
    private const string Definicoes = "$defs";
    private const string PrefixoDoPonteiroDeDefinicao = "/$defs/";
    private const string SufixoDoArquivo = ".schema.json";
    private const string PrefixoDosComponentes = "#/components/schemas/";

    private static readonly string[] PalavrasQueNaoEntram = ["$id", "$schema", Definicoes];

    private readonly IReadOnlyDictionary<string, JsonNode> _arquivos;

    public ComponentesDoEsquema(IReadOnlyDictionary<string, JsonNode> arquivos) => _arquivos = arquivos;

    /// <summary>Os componentes pelo nome, em ordem ordinal.</summary>
    public SortedDictionary<string, JsonNode> Gerar()
    {
        SortedDictionary<string, JsonNode> componentes = new(StringComparer.Ordinal);
        foreach ((string nome, Parte parte) in NomearAsAlcancadas())
        {
            componentes[nome] = Componente(parte);
        }

        return componentes;
    }

    private Dictionary<string, Parte> NomearAsAlcancadas()
    {
        Dictionary<string, Parte> nomeadas = new(StringComparer.Ordinal);
        foreach (Parte parte in Alcancadas())
        {
            if (!nomeadas.TryAdd(Nome(parte), parte))
            {
                throw new InvalidOperationException(
                    $"'{nomeadas[Nome(parte)].Localizacao}' e '{parte.Localizacao}' dariam o mesmo componente '{Nome(parte)}'.");
            }
        }

        return nomeadas;
    }

    private HashSet<Parte> Alcancadas()
    {
        HashSet<Parte> alcancadas = [];
        Queue<Parte> pendentes = new([new Parte(ArquivosDoEsquema.ArquivoRaiz, Definicao: null)]);
        while (pendentes.TryDequeue(out Parte parte))
        {
            if (alcancadas.Add(parte))
            {
                ReferenciasDe(Conteudo(parte), parte.Arquivo).ToList().ForEach(pendentes.Enqueue);
            }
        }

        return alcancadas;
    }

    private JsonNode Componente(Parte parte)
    {
        JsonNode conteudo = Conteudo(parte);
        foreach (JsonObject objeto in ObjetosComReferencia(conteudo).ToList())
        {
            objeto[Referencia] = PrefixoDosComponentes + Nome(Alvo(parte.Arquivo, (string)objeto[Referencia]!));
        }

        return conteudo;
    }

    private static IEnumerable<Parte> ReferenciasDe(JsonNode conteudo, string arquivoDeOrigem) =>
        ObjetosComReferencia(conteudo).Select(objeto => Alvo(arquivoDeOrigem, (string)objeto[Referencia]!));

    // Uma cópia sem as palavras que só valem no arquivo de origem; nunca o nó do esquema.
    private JsonNode Conteudo(Parte parte)
    {
        JsonNode arquivo = _arquivos.TryGetValue(parte.Arquivo, out JsonNode? lido)
            ? lido
            : throw new InvalidOperationException($"O arquivo '{parte.Arquivo}' não existe no esquema.");
        JsonNode origem = parte.Definicao is null
            ? arquivo
            : arquivo[Definicoes]?[parte.Definicao] ?? throw new InvalidOperationException($"A definição '{parte.Localizacao}' não existe no esquema.");

        JsonNode copia = origem.DeepClone();
        if (copia is JsonObject objeto)
        {
            Array.ForEach(PalavrasQueNaoEntram, palavra => objeto.Remove(palavra));
        }

        return copia;
    }

    private static Parte Alvo(string arquivoDeOrigem, string referencia)
    {
        string[] partes = referencia.Split('#', 2);
        string arquivo = partes[0].Length == 0 ? arquivoDeOrigem : partes[0];
        string ponteiro = partes.Length == 2 ? partes[1] : string.Empty;

        return new Parte(arquivo, ponteiro.Length == 0 ? null : DefinicaoDoPonteiro(referencia, ponteiro));
    }

    private static string DefinicaoDoPonteiro(string referencia, string ponteiro) =>
        ponteiro.StartsWith(PrefixoDoPonteiroDeDefinicao, StringComparison.Ordinal) && !ponteiro[PrefixoDoPonteiroDeDefinicao.Length..].Contains('/', StringComparison.Ordinal)
            ? ponteiro[PrefixoDoPonteiroDeDefinicao.Length..]
            : throw new InvalidOperationException($"A referência '{referencia}' não é para a raiz nem para uma definição de '$defs'.");

    private static IEnumerable<JsonObject> ObjetosComReferencia(JsonNode? no) => no switch
    {
        JsonObject objeto => ObjetosComReferencia(objeto),
        JsonArray lista => lista.SelectMany(ObjetosComReferencia),
        _ => [],
    };

    private static IEnumerable<JsonObject> ObjetosComReferencia(JsonObject objeto)
    {
        if (objeto[Referencia] is JsonValue valor && valor.TryGetValue(out string? _))
        {
            yield return objeto;
        }

        foreach (JsonObject descendente in objeto.SelectMany(static propriedade => ObjetosComReferencia(propriedade.Value)))
        {
            yield return descendente;
        }
    }

    private static string Nome(Parte parte)
    {
        string arquivo = parte.Arquivo.EndsWith(SufixoDoArquivo, StringComparison.Ordinal)
            ? parte.Arquivo[..^SufixoDoArquivo.Length]
            : parte.Arquivo;

        return (parte.Arquivo, parte.Definicao) switch
        {
            (ArquivosDoEsquema.ArquivoRaiz, null) => EsquemaPublicadoDoDocumento.Componente,
            (_, null) => EsquemaPublicadoDoDocumento.Componente + PascalCase(arquivo),
            (_, { } definicao) => EsquemaPublicadoDoDocumento.Componente + PascalCase(arquivo) + PascalCase(definicao),
        };
    }

    private static string PascalCase(string texto) =>
        string.Concat(texto.Split('-').Select(static parte => string.Concat(parte[..1].ToUpper(CultureInfo.InvariantCulture), parte[1..])));

    private readonly record struct Parte(string Arquivo, string? Definicao)
    {
        public string Localizacao => $"{Arquivo}#{(Definicao is null ? string.Empty : PrefixoDoPonteiroDeDefinicao + Definicao)}";
    }
}
