namespace Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Publica o esquema do documento de configuração como componentes da especificação OpenAPI do módulo, para que o contrato
/// REST o descreva e o cliente gerado o tipe. O texto dos componentes vem de <see cref="ComponentesDoEsquema"/> e é escrito
/// como o esquema o declara (<see cref="ComponenteDoEsquemaPublicado"/>), sem passar pelo leitor da biblioteca. Um componente
/// que o documento já tem com o mesmo nome não é trocado: a geração do documento falha.
/// </summary>
public sealed class EsquemaDoDocumentoNaOpenApi : IOpenApiDocumentTransformer
{
    // Gerados uma vez, na criação do singleton: o nó nunca é alterado, e cada documento recebe componentes próprios sobre ele.
    private readonly SortedDictionary<string, JsonNode> _componentes = new ComponentesDoEsquema(ArquivosDoEsquema.DoAssembly().PorNome).Gerar();

    /// <exception cref="InvalidOperationException">O documento já tem um componente com o nome de um componente do esquema.</exception>
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        foreach ((string nome, JsonNode conteudo) in _componentes)
        {
            Publicar(document, nome, new ComponenteDoEsquemaPublicado(conteudo));
        }

        return Task.CompletedTask;
    }

    private static void Publicar(OpenApiDocument documento, string nome, ComponenteDoEsquemaPublicado componente)
    {
        if (!documento.AddComponent<IOpenApiSchema>(nome, componente))
        {
            throw new InvalidOperationException(
                $"O componente '{nome}' do esquema do documento de configuração não pode entrar no documento OpenAPI: o documento já tem um componente com esse nome.");
        }
    }
}
