namespace Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

/// <summary>
/// Lê do esquema, e não de uma lista escrita à mão, o que a normalização precisa: quais campos são só de leitura e quais
/// partes exigem chave local. Percorre <c>properties</c>, <c>items</c>, <c>$ref</c>, <c>allOf</c>, <c>anyOf</c>, <c>oneOf</c>
/// e os ramos <c>then</c>/<c>else</c>, e junta o que cada trecho declara para o mesmo ponto do documento. O esquema é
/// recursivo, então cada ponto é montado uma vez, na primeira vez que seus descendentes são lidos.
/// </summary>
internal sealed class FormaLidaDoEsquema
{
    private static readonly string[] PalavrasComSubesquemas = ["allOf", "anyOf", "oneOf"];
    private static readonly string[] PalavrasComRamo = ["then", "else"];

    private readonly ArquivosDoEsquema _arquivos;
    private readonly ConcurrentDictionary<string, FormaDoNo> _montados = new(StringComparer.Ordinal);

    public FormaLidaDoEsquema(ArquivosDoEsquema arquivos)
    {
        _arquivos = arquivos;
        Raiz = Montar([arquivos.Raiz(ArquivosDoEsquema.ArquivoRaiz)]);
    }

    public FormaDoNo Raiz { get; }

    private FormaDoNo Montar(IEnumerable<TrechoDoEsquema> partida)
    {
        IReadOnlyList<TrechoDoEsquema> trechos = Expandir(partida);
        string[] localizacoes = [.. trechos.Select(static trecho => trecho.Localizacao)];
        return _montados.GetOrAdd(
            string.Join('|', localizacoes),
            _ => new FormaDoNo(
                localizacoes,
                trechos.Any(static trecho => trecho.DeclaraVerdadeiro("readOnly")),
                ParteDe(trechos),
                () => PropriedadesDe(trechos),
                () => ItensDe(trechos)));
    }

    private Dictionary<string, FormaDoNo> PropriedadesDe(IReadOnlyList<TrechoDoEsquema> trechos) =>
        trechos.SelectMany(static trecho => PropriedadesDeclaradas(trecho))
            .GroupBy(static propriedade => propriedade.Nome, StringComparer.Ordinal)
            .ToDictionary(static grupo => grupo.Key, grupo => Montar(grupo.Select(static propriedade => propriedade.Trecho)), StringComparer.Ordinal);

    private FormaDoNo? ItensDe(IReadOnlyList<TrechoDoEsquema> trechos)
    {
        TrechoDoEsquema[] itens = [.. trechos.Select(static trecho => trecho.PalavraChave("items") is { } no ? trecho.Filho("items", no) : null).OfType<TrechoDoEsquema>()];
        return itens.Length == 0 ? null : Montar(itens);
    }

    private static IEnumerable<(string Nome, TrechoDoEsquema Trecho)> PropriedadesDeclaradas(TrechoDoEsquema trecho) =>
        trecho.PalavraChave("properties") is JsonObject propriedades
            ? propriedades.Where(static propriedade => propriedade.Value is not null)
                .Select(propriedade => (propriedade.Key, trecho.Filho($"properties/{propriedade.Key}", propriedade.Value!)))
            : [];

    private List<TrechoDoEsquema> Expandir(IEnumerable<TrechoDoEsquema> partida)
    {
        List<TrechoDoEsquema> todos = [];
        foreach (TrechoDoEsquema trecho in partida)
        {
            Acrescentar(trecho, todos);
        }

        return todos;
    }

    private void Acrescentar(TrechoDoEsquema trecho, List<TrechoDoEsquema> todos)
    {
        if (todos.Exists(existente => existente.Localizacao == trecho.Localizacao))
        {
            return;
        }

        todos.Add(trecho);
        foreach (TrechoDoEsquema relacionada in Relacionadas(trecho))
        {
            Acrescentar(relacionada, todos);
        }
    }

    private IEnumerable<TrechoDoEsquema> Relacionadas(TrechoDoEsquema trecho)
    {
        if (trecho.PalavraChave("$ref") is JsonValue referencia && referencia.TryGetValue(out string? destino))
        {
            yield return _arquivos.Resolver(trecho, destino);
        }

        foreach (TrechoDoEsquema subesquema in PalavrasComSubesquemas.SelectMany(palavra => Subesquemas(trecho, palavra)))
        {
            yield return subesquema;
        }

        foreach (string palavra in PalavrasComRamo.Where(palavra => trecho.PalavraChave(palavra) is JsonObject))
        {
            yield return trecho.Filho(palavra, trecho.PalavraChave(palavra)!);
        }
    }

    private static IEnumerable<TrechoDoEsquema> Subesquemas(TrechoDoEsquema trecho, string palavra) =>
        trecho.PalavraChave(palavra) is JsonArray lista
            ? lista.Where(static subesquema => subesquema is not null)
                .Select((subesquema, indice) => trecho.Filho(string.Create(CultureInfo.InvariantCulture, $"{palavra}/{indice}"), subesquema!))
            : [];

    private ParteComChave? ParteDe(IReadOnlyList<TrechoDoEsquema> trechos)
    {
        TrechoDoEsquema? definidor = trechos.FirstOrDefault(DeclaraChaveObrigatoria);
        return definidor is null ? null : new ParteComChave(TipoDaParte(definidor), EscopoDaChaveDe(definidor));
    }

    // A chave de uma subparte (item de uma etapa) só é única dentro da parte que a contém; a de uma parte de nível próprio
    // (a etapa, a fase, a banca) é única no documento.
    private EscopoDaChave EscopoDaChaveDe(TrechoDoEsquema definidor) =>
        definidor.Ponteiro.Length > 0 && DeclaraChaveObrigatoria(_arquivos.Raiz(definidor.Arquivo))
            ? EscopoDaChave.ParteAncestral
            : EscopoDaChave.Documento;

    private static bool DeclaraChaveObrigatoria(TrechoDoEsquema trecho) =>
        trecho.PalavraChave("required") is JsonArray obrigatorias
        && obrigatorias.Any(static nome => nome is JsonValue valor && valor.TryGetValue(out string? texto) && texto == "chave")
        && trecho.PalavraChave("properties") is JsonObject propriedades
        && propriedades.ContainsKey("chave");

    // O título da propriedade <c>chave</c> diz de que parte ela é ("Chave da etapa"); dele sai o tipo da parte.
    private static string TipoDaParte(TrechoDoEsquema definidor)
    {
        string? titulo = ((JsonObject)definidor.PalavraChave("properties")!)["chave"]?["title"]?.GetValue<string>();
        string[] palavras = titulo?.Split(' ', 3) ?? [];
        if (palavras.Length != 3 || palavras[0] != "Chave")
        {
            throw new InvalidOperationException($"O título da chave em {definidor.Localizacao} deve ser 'Chave de <parte>'.");
        }

        return SemAcento(palavras[2]).ToLowerInvariant().Replace(' ', '-');
    }

    private static string SemAcento(string texto) => new(
        [.. texto.Normalize(NormalizationForm.FormD).Where(static letra => char.GetUnicodeCategory(letra) != UnicodeCategory.NonSpacingMark)]);
}
