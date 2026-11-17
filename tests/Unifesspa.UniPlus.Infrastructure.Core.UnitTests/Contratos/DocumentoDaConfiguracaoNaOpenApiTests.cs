namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

/// <summary>
/// O esquema do documento de configuração, como o baseline da OpenAPI do módulo Seleção o publica, é o esquema dos arquivos
/// de <c>contracts/selecao/configuracao/v1</c> sem alteração, afora a troca das referências, e o contrato REST o referencia.
/// A regra de transformação é escrita aqui por conta própria, para não repetir a do gerador.
/// </summary>
public sealed class DocumentoDaConfiguracaoNaOpenApiTests
{
    private const string Prefixo = "DocumentoDaConfiguracao";
    private const string RaizDoEsquema = "documento.schema.json";
    private const string PrefixoDasReferencias = "#/components/schemas/";

    private static readonly JsonNode Baseline = JsonNode.Parse(File.ReadAllText(Path.Join(AppContext.BaseDirectory, "Contratos", "openapi.selecao.json")))!;

    private static JsonObject ComponentesDoBaseline => Baseline["components"]!["schemas"]!.AsObject();

    [Fact(DisplayName = "Cada componente do esquema é igual ao trecho de origem, sem $id, $schema e $defs e com as referências trocadas")]
    public void Componente_ECopiaDoTrechoDeOrigem()
    {
        foreach ((string nome, JsonNode esperado) in ComponentesEsperados())
        {
            JsonNode? publicado = ComponentesDoBaseline[nome];

            JsonNode.DeepEquals(publicado, esperado).Should().BeTrue($"{nome} é copia do esquema de origem");
        }
    }

    [Fact(DisplayName = "Os componentes do esquema no baseline são exatamente o que as referências a partir do documento alcançam")]
    public void Componentes_SaoOFechoAPartirDaRaiz()
    {
        string[] publicados = [.. ComponentesDoBaseline.Select(static componente => componente.Key).Where(static nome => nome.StartsWith(Prefixo, StringComparison.Ordinal))];

        publicados.Should().BeEquivalentTo(ComponentesEsperados().Keys);
    }

    [Fact(DisplayName = "Toda referência dos componentes do esquema aponta para um componente que existe")]
    public void Referencias_ApontamParaComponentesExistentes()
    {
        foreach ((string nome, JsonNode? componente) in ComponentesDoBaseline.Where(static par => par.Key.StartsWith(Prefixo, StringComparison.Ordinal)))
        {
            foreach (string referencia in Referencias(componente))
            {
                referencia.Should().StartWith(PrefixoDasReferencias, nome);
                ComponentesDoBaseline.ContainsKey(referencia[PrefixoDasReferencias.Length..]).Should().BeTrue($"{nome} referencia {referencia}");
            }
        }
    }

    [Fact(DisplayName = "O corpo do POST e do PUT e a resposta 200 do GET referenciam o componente do documento")]
    public void Contrato_ReferenciaOComponenteDoDocumento()
    {
        JsonNode lista = Operacoes("/rascunhos-de-processos-seletivos");
        JsonNode item = Operacoes("/rascunhos-de-processos-seletivos/{id}");

        SchemaDe(lista["post"]!["requestBody"]!["content"]!["application/json"]).Should().Be(RaizDoComponente);
        SchemaDe(item["put"]!["requestBody"]!["content"]!["application/json"]).Should().Be(RaizDoComponente);
        JsonObject conteudoDoGet = item["get"]!["responses"]!["200"]!["content"]!.AsObject();
        conteudoDoGet.Select(static media => media.Key).Should().ContainSingle().Which.Should().StartWith("application/vnd.uniplus.rascunho-de-processo-seletivo.v");
        SchemaDe(conteudoDoGet.Single().Value).Should().Be(RaizDoComponente);
    }

    private static string RaizDoComponente => PrefixoDasReferencias + Prefixo;

    private static string? SchemaDe(JsonNode? media) => media?["schema"]?["$ref"]?.GetValue<string>();

    private static JsonNode Operacoes(string caminho) =>
        Baseline["paths"]!.AsObject().Single(par => par.Key.EndsWith(caminho, StringComparison.Ordinal)).Value!;

    private static IEnumerable<string> Referencias(JsonNode? no) => no switch
    {
        JsonObject objeto => ReferenciaDe(objeto).Concat(objeto.SelectMany(static propriedade => Referencias(propriedade.Value))),
        JsonArray lista => lista.SelectMany(Referencias),
        _ => [],
    };

    private static IEnumerable<string> ReferenciaDe(JsonObject objeto) =>
        objeto["$ref"] is JsonValue valor && valor.TryGetValue(out string? referencia) ? [referencia] : [];

    private static SortedDictionary<string, JsonNode> ComponentesEsperados()
    {
        SortedDictionary<string, JsonNode> esperados = new(StringComparer.Ordinal);
        foreach ((string arquivo, string? definicao) in Alcancados())
        {
            esperados[Nome(arquivo, definicao)] = TrechoSemIdentidade(arquivo, definicao);
        }

        return esperados;
    }

    // A partir da raiz do documento, o que cada referência alcança: o arquivo inteiro ou uma definição dele.
    private static HashSet<(string Arquivo, string? Definicao)> Alcancados()
    {
        HashSet<(string, string?)> vistos = [];
        Queue<(string, string?)> pendentes = new([(RaizDoEsquema, null)]);
        while (pendentes.TryDequeue(out (string Arquivo, string? Definicao) parte))
        {
            if (vistos.Add(parte))
            {
                Referencias(TrechoDeOrigem(parte.Arquivo, parte.Definicao)).ToList().ForEach(referencia => pendentes.Enqueue(Alvo(parte.Arquivo, referencia)));
            }
        }

        return vistos;
    }

    private static JsonNode TrechoSemIdentidade(string arquivo, string? definicao) =>
        TrocarReferencias(TrechoDeOrigem(arquivo, definicao), arquivo);

    // Ainda com as referências do arquivo: é por elas que se alcança o que o documento usa.
    private static JsonNode TrechoDeOrigem(string arquivo, string? definicao)
    {
        JsonNode trecho = (definicao is null ? ContratoDaConfiguracao.Esquema(arquivo) : ContratoDaConfiguracao.Esquema(arquivo)["$defs"]![definicao]!).DeepClone();
        if (definicao is null)
        {
            JsonObject raiz = trecho.AsObject();
            new[] { "$id", "$schema", "$defs" }.ToList().ForEach(palavra => raiz.Remove(palavra));
        }

        return trecho;
    }

    private static JsonNode TrocarReferencias(JsonNode no, string arquivo)
    {
        foreach (JsonObject objeto in ObjetosComReferencia(no).ToList())
        {
            (string arquivoAlvo, string? definicaoAlvo) = Alvo(arquivo, objeto["$ref"]!.GetValue<string>());
            objeto["$ref"] = PrefixoDasReferencias + Nome(arquivoAlvo, definicaoAlvo);
        }

        return no;
    }

    private static IEnumerable<JsonObject> ObjetosComReferencia(JsonNode? no) => no switch
    {
        JsonObject objeto => (objeto["$ref"] is JsonValue ? [objeto] : Enumerable.Empty<JsonObject>()).Concat(objeto.SelectMany(static propriedade => ObjetosComReferencia(propriedade.Value))),
        JsonArray lista => lista.SelectMany(ObjetosComReferencia),
        _ => [],
    };

    private static (string Arquivo, string? Definicao) Alvo(string arquivoDeOrigem, string referencia)
    {
        string[] partes = referencia.Split('#', 2);
        string arquivo = partes[0].Length == 0 ? arquivoDeOrigem : partes[0];
        string ponteiro = partes.Length == 2 ? partes[1] : string.Empty;

        return (arquivo, ponteiro.Length == 0 ? null : ponteiro["/$defs/".Length..]);
    }

    private static string Nome(string arquivo, string? definicao)
    {
        string sufixoDoArquivo = arquivo == RaizDoEsquema ? string.Empty : PascalCase(arquivo.Replace(".schema.json", string.Empty, StringComparison.Ordinal));
        return definicao is null ? Prefixo + sufixoDoArquivo : Prefixo + PascalCase(arquivo.Replace(".schema.json", string.Empty, StringComparison.Ordinal)) + PascalCase(definicao);
    }

    private static string PascalCase(string texto) =>
        string.Concat(texto.Split('-').Select(static parte => char.ToUpperInvariant(parte[0]) + parte[1..]));
}
