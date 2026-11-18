namespace Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

using System.Reflection;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

/// <summary>
/// Os arquivos do esquema do documento de configuração, lidos do assembly onde viajam embutidos, e a navegação entre eles
/// pelas referências <c>$ref</c>, que são sempre relativas e locais.
/// </summary>
internal sealed class ArquivosDoEsquema
{
    internal const string ArquivoRaiz = "documento.schema.json";

    private const string PrefixoDosRecursos = "Unifesspa.UniPlus.Selecao.Contratos.Configuracao.v1.";

    private readonly Dictionary<string, JsonNode> _arquivos;

    private ArquivosDoEsquema(Dictionary<string, JsonNode> arquivos) => _arquivos = arquivos;

    /// <summary>Os arquivos pelo nome (<c>etapas.schema.json</c>).</summary>
    public IReadOnlyDictionary<string, JsonNode> PorNome => _arquivos;

    public static ArquivosDoEsquema DoAssembly()
    {
        Assembly assembly = typeof(ArquivosDoEsquema).Assembly;
        Dictionary<string, JsonNode> lidos = assembly.GetManifestResourceNames()
            .Where(static nome => nome.StartsWith(PrefixoDosRecursos, StringComparison.Ordinal))
            .ToDictionary(
                static nome => nome[PrefixoDosRecursos.Length..],
                nome => Ler(assembly, nome),
                StringComparer.Ordinal);

        return new ArquivosDoEsquema(lidos);
    }

    public TrechoDoEsquema Raiz(string arquivo) => new(arquivo, string.Empty, _arquivos[arquivo]);

    /// <summary>Resolve uma referência relativa, dentro do arquivo de origem ou para outro arquivo do esquema.</summary>
    public TrechoDoEsquema Resolver(TrechoDoEsquema origem, string referencia)
    {
        string[] partes = referencia.Split('#', 2);
        string arquivo = partes[0].Length == 0 ? origem.Arquivo : partes[0];
        string ponteiro = partes.Length == 2 ? partes[1] : string.Empty;

        return new TrechoDoEsquema(arquivo, ponteiro, PonteiroJson.Resolver(_arquivos[arquivo], ponteiro)!);
    }

    /// <summary>Localiza o trecho do esquema que a avaliação cita pela URI absoluta (<c>.../etapas.schema.json#/properties/nome</c>).</summary>
    public TrechoDoEsquema Localizar(Uri localizacao)
    {
        string arquivo = Path.GetFileName(localizacao.AbsolutePath);
        string ponteiro = Uri.UnescapeDataString(localizacao.Fragment.TrimStart('#'));
        return Resolver(Raiz(arquivo), $"#{ponteiro}");
    }

    private static JsonNode Ler(Assembly assembly, string recurso)
    {
        using Stream fluxo = assembly.GetManifestResourceStream(recurso)!;
        return JsonNode.Parse(fluxo)!;
    }
}
