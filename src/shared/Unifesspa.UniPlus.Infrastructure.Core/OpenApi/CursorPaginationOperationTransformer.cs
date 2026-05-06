namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using System.Reflection;
using System.Text.Json.Nodes;

using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Pagination;

/// <summary>
/// Operation transformer que reconcilia o spec OpenAPI com o contrato wire de
/// paginação por cursor opaco (ADR-0026). O <see cref="FromCursorAttribute"/>
/// herda de <c>ModelBinderAttribute</c> com <c>BindingSource = Query</c>, fazendo
/// o ApiExplorer descrever o tipo <see cref="PageRequest"/> (record com
/// <c>AfterId</c> + <c>Limit</c>) como query bag — vazando o shape interno
/// pós-decode em vez do contrato wire (<c>cursor</c> + <c>limit</c>).
/// <para>
/// Este transformer detecta operações com <see cref="FromCursorAttribute"/> e:
/// </para>
/// <list type="number">
///   <item><description>Remove os parâmetros vazados <c>AfterId</c> e <c>Limit</c>.</description></item>
///   <item><description>Adiciona <c>cursor</c> (string, opcional, opaca) e <c>limit</c> (int, opcional) como query params.</description></item>
///   <item><description>Declara os headers <c>Link</c> (RFC 5988/8288) e <c>X-Page-Size</c> em respostas 200 — espelha o que <c>PaginationControllerExtensions.OkPaginatedAsync</c> de fato emite.</description></item>
///   <item><description>Marca a operação com a extension <c>x-uniplus-paginated: true</c> para clientes detectarem o pattern.</description></item>
/// </list>
/// </summary>
public sealed class CursorPaginationOperationTransformer : IOpenApiOperationTransformer
{
    private const string CursorParam = "cursor";
    private const string LimitParam = "limit";
    private const string LinkHeader = "Link";
    private const string PageSizeHeader = "X-Page-Size";
    private const string PaginatedExtension = "x-uniplus-paginated";
    private const string OkStatus = "200";

    private static readonly string[] LeakedPageRequestProperties = ["AfterId", "Limit"];

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        if (ResolveCursorAttribute(context) is null)
            return Task.CompletedTask;

        RemoveLeakedPageRequestParameters(operation);
        AddWireParameters(operation);
        DeclareResponseHeaders(operation);
        MarkAsPaginated(operation);

        return Task.CompletedTask;
    }

    private static FromCursorAttribute? ResolveCursorAttribute(OpenApiOperationTransformerContext context)
    {
        // Espelha a estratégia do PageRequestModelBinder.ResolveAttribute:
        // ControllerParameterDescriptor.ParameterInfo é o caminho confiável
        // para chegar ao FromCursorAttribute (BindingInfo.BinderType só guarda
        // o tipo do binder, não o atributo em si).
        if (context.Description.ActionDescriptor is not ControllerActionDescriptor descriptor)
            return null;

        foreach (ParameterDescriptor parameter in descriptor.Parameters)
        {
            if (parameter is not ControllerParameterDescriptor controllerParam)
                continue;

            if (controllerParam.ParameterType != typeof(PageRequest))
                continue;

            FromCursorAttribute? attribute = controllerParam.ParameterInfo
                .GetCustomAttribute<FromCursorAttribute>();
            if (attribute is not null)
                return attribute;
        }

        return null;
    }

    private static void RemoveLeakedPageRequestParameters(OpenApiOperation operation)
    {
        if (operation.Parameters is null || operation.Parameters.Count == 0)
            return;

        // Remove só parâmetros In = Query cujo Name bate exatamente com
        // propriedades do PageRequest. Mantém qualquer outro query param
        // explícito do endpoint (ex.: filtros adicionais).
        operation.Parameters = operation.Parameters
            .Where(p => !IsLeakedPageRequestProperty(p))
            .ToList();
    }

    private static bool IsLeakedPageRequestProperty(IOpenApiParameter parameter) =>
        parameter.In == ParameterLocation.Query
            && LeakedPageRequestProperties.Contains(parameter.Name, StringComparer.Ordinal);

    private static void AddWireParameters(OpenApiOperation operation)
    {
        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = CursorParam,
            In = ParameterLocation.Query,
            Required = false,
            Description = "Cursor opaco AES-GCM emitido pelo servidor no header Link da página anterior. "
                + "Ausente na primeira página. Cliente trata como string opaca — não decodificar (ADR-0026, ADR-0031).",
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
            },
        });
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = LimitParam,
            In = ParameterLocation.Query,
            Required = false,
            Description = "Tamanho máximo da janela de resultados. Limites configurados em "
                + "CursorPaginationOptions; valores fora do range retornam 422 com "
                + "code uniplus.pagination.limit_invalido (ADR-0026).",
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.Integer,
                Format = "int32",
            },
        });
    }

    private static void DeclareResponseHeaders(OpenApiOperation operation)
    {
        if (operation.Responses is null)
            return;

        if (!operation.Responses.TryGetValue(OkStatus, out IOpenApiResponse? response200)
            || response200 is not OpenApiResponse okResponse)
            return;

        okResponse.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal);
        okResponse.Headers[LinkHeader] = new OpenApiHeader
        {
            Description = "Links de navegação da paginação (RFC 5988/8288). "
                + "rel=\"self\" sempre presente; rel=\"next\" só quando há próxima página. "
                + "Cada link carrega o cursor opaco no parâmetro `cursor` (ADR-0026).",
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
            },
        };
        okResponse.Headers[PageSizeHeader] = new OpenApiHeader
        {
            Description = "Quantidade de itens retornados na página atual (sempre menor ou igual ao limit efetivo).",
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.Integer,
                Format = "int32",
            },
        };
    }

    private static void MarkAsPaginated(OpenApiOperation operation)
    {
        operation.Extensions ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);
        operation.Extensions[PaginatedExtension] = new JsonNodeExtension(JsonValue.Create(true));
    }
}
