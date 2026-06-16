namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Document transformer que remove o schema órfão <c>PaginationDirection</c>
/// das <c>components.schemas</c>. O enum <see cref="Pagination.PaginationDirection"/>
/// é propriedade interna de <c>PageRequest</c>/<c>CursorPayload</c> (pós-decode),
/// nunca serializada em corpo de request/response — o ApiExplorer ainda registra
/// um schema para ela ao descrever o <c>PageRequest</c> como query bag.
/// <para>
/// O <see cref="CursorPaginationOperationTransformer"/> já remove o parâmetro
/// vazado <c>Direction</c> e declara o wire param <c>direction</c> com schema
/// inline (string enum <c>next</c>/<c>prev</c>) — logo o schema de componente
/// fica sem nenhuma referência (<c>$ref</c>). Schema órfão não só é ruído no
/// contrato como diverge entre módulos (cada API serializa enums de forma
/// distinta: string vs integer), quebrando a fitness function da ADR-0035.
/// </para>
/// <para>
/// Remoção cirúrgica por nome, espelhando a filosofia do operation transformer
/// (que poda <c>AfterId</c>/<c>Limit</c>/<c>Direction</c> por nome exato).
/// </para>
/// </summary>
public sealed class PaginationOrphanSchemaDocumentTransformer : IOpenApiDocumentTransformer
{
    private const string OrphanSchemaName = "PaginationDirection";

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);

        document.Components?.Schemas?.Remove(OrphanSchemaName);

        return Task.CompletedTask;
    }
}
