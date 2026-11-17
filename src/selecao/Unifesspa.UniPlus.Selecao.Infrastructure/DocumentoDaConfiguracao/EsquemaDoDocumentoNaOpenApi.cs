namespace Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Publica o esquema do documento de configuração como componentes da especificação OpenAPI do módulo, para que o contrato
/// REST o descreva e o cliente gerado o tipe. O texto dos componentes vem de <see cref="ComponentesDoEsquema"/> e é escrito
/// como o esquema o declara (<see cref="ComponenteDoEsquemaPublicado"/>), sem passar pelo leitor da biblioteca.
/// </summary>
public sealed class EsquemaDoDocumentoNaOpenApi : IOpenApiDocumentTransformer
{
    // Gerados uma vez, na criação do singleton: o nó nunca é alterado, e cada documento recebe componentes próprios sobre ele.
    private readonly SortedDictionary<string, JsonNode> _componentes = new ComponentesDoEsquema(ArquivosDoEsquema.DoAssembly().PorNome).Gerar();

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Components ??= new OpenApiComponents();
        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        foreach ((string nome, JsonNode conteudo) in _componentes)
        {
            document.Components.Schemas[nome] = new ComponenteDoEsquemaPublicado(conteudo);
        }

        return Task.CompletedTask;
    }
}
