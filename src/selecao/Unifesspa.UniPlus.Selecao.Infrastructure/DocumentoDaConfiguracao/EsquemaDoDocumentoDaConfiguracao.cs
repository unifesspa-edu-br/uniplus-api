namespace Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

using System.Text.Json;
using System.Text.Json.Nodes;

using Json.Schema;

using Kernel.Results;

using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

/// <summary>
/// O esquema do documento de configuração, lido do assembly e montado uma vez. Todos os arquivos ficam num registro
/// próprio, de modo que nenhuma referência é buscada fora e o registro de esquemas da plataforma nunca é consultado.
/// </summary>
public sealed class EsquemaDoDocumentoDaConfiguracao : IEsquemaDoDocumentoDaConfiguracao
{
    private static readonly EvaluationOptions OpcoesDaAvaliacao = new() { RequireFormatValidation = true, OutputFormat = OutputFormat.Hierarchical };

    // O registro de esquemas da biblioteca não é feito para uso concorrente; a avaliação percorre esse registro nas
    // referências, então as avaliações se alternam em vez de se sobrepor.
    private readonly Lock _trava = new();
    private readonly JsonSchema _esquema;
    private readonly ArquivosDoEsquema _arquivos;

    public EsquemaDoDocumentoDaConfiguracao()
    {
        _arquivos = ArquivosDoEsquema.DoAssembly();
        _esquema = MontarOEsquema(_arquivos);
        Forma = new FormaLidaDoEsquema(_arquivos).Raiz;
    }

    public FormaDoNo Forma { get; }

    public IReadOnlyList<FieldError> Avaliar(JsonObject documento)
    {
        ArgumentNullException.ThrowIfNull(documento);

        EvaluationResults resultado;
        lock (_trava)
        {
            resultado = _esquema.Evaluate(JsonSerializer.SerializeToElement(documento), OpcoesDaAvaliacao);
        }

        return resultado.IsValid ? [] : ViolacoesDoEsquema.Causas(resultado, _arquivos, documento);
    }

    private static JsonSchema MontarOEsquema(ArquivosDoEsquema arquivos)
    {
        // Fetch devolve sempre nulo: uma referência que os arquivos embutidos não resolvem falha, em vez de ir buscar na rede.
        BuildOptions construcao = new() { SchemaRegistry = new SchemaRegistry { Fetch = static (_, _) => null } };
        Dictionary<string, JsonSchema> montados = arquivos.PorNome.ToDictionary(
            static arquivo => arquivo.Key,
            arquivo => JsonSchema.FromText(arquivo.Value.ToJsonString(), construcao),
            StringComparer.Ordinal);

        return montados[ArquivosDoEsquema.ArquivoRaiz];
    }
}
