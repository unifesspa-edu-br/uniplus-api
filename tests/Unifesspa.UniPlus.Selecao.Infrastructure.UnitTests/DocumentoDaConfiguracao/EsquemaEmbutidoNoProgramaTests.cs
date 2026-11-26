namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

/// <summary>
/// Os esquemas do documento de configuração viajam dentro do programa publicado. Sem eles, o documento OpenAPI da
/// Seleção responde erro 500, e o build não acusa nada quando a pasta de contratos não está no contexto da imagem.
/// </summary>
public sealed class EsquemaEmbutidoNoProgramaTests
{
    private static readonly string PastaDosEsquemas =
        Path.Join(AppContext.BaseDirectory, "Contratos", "selecao-configuracao", "v1");

    [Fact(DisplayName = "Todo arquivo de esquema da pasta de contratos está embutido no programa, inclusive o documento raiz")]
    public void CadaArquivoDeEsquemaDaPastaDeContratosEstaEmbutido()
    {
        string[] naPasta = [.. Directory.GetFiles(PastaDosEsquemas, "*.schema.json").Select(Path.GetFileName).Select(static nome => nome!)];

        IReadOnlyDictionary<string, JsonNode> embutidos = ArquivosDoEsquema.DoAssembly().PorNome;

        naPasta.Should().Contain(ArquivosDoEsquema.ArquivoRaiz);
        embutidos.Keys.Should().BeEquivalentTo(naPasta);
    }

    [Fact(DisplayName = "Toda referência de esquema de cada arquivo embutido aponta para um arquivo e um trecho que existem")]
    public void Toda_ReferenciaDosArquivosEmbutidosEResolvida()
    {
        ArquivosDoEsquema arquivos = ArquivosDoEsquema.DoAssembly();

        foreach ((string nome, JsonNode arquivo) in arquivos.PorNome)
        {
            foreach (string referencia in ReferenciasDe(arquivo))
            {
                JsonNode? trecho = ResolverSemFalhar(arquivos, nome, referencia);
                trecho.Should().NotBeNull($"a referência '{referencia}' de {nome} deve apontar para um trecho dos esquemas embutidos");
            }
        }
    }

    private static JsonNode? ResolverSemFalhar(ArquivosDoEsquema arquivos, string nome, string referencia)
    {
        try
        {
            return arquivos.Resolver(arquivos.Raiz(nome), referencia).No;
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private static IEnumerable<string> ReferenciasDe(JsonNode? no) => no switch
    {
        JsonObject objeto => objeto.SelectMany(static par =>
            par.Key == "$ref" && par.Value is JsonValue valor ? [valor.GetValue<string>()] : ReferenciasDe(par.Value)),
        JsonArray lista => lista.SelectMany(ReferenciasDe),
        _ => [],
    };
}
