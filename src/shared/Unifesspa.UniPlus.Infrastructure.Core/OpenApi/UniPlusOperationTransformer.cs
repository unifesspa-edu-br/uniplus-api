namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using System.Text.Json.Nodes;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Idempotency;

/// <summary>
/// Operation transformer que injeta metadata Uni+ por operação:
/// <list type="bullet">
///   <item><description>Header <c>Idempotency-Key</c> declarado como required quando a action tem <c>[RequiresIdempotencyKey]</c>.</description></item>
///   <item><description>Extension <c>x-uniplus-idempotent: true</c> em endpoints com idempotência opt-in.</description></item>
///   <item><description>Content type de respostas 4xx/5xx coagido para <c>application/problem+json</c> — espelha o que o middleware de fato emite via <c>result.ToActionResult(mapper)</c> (ADR-0023, RFC 9457). Sem isso o spec declararia <c>application/json</c> (default do MVC) e clientes gerados rejeitariam o response real.</description></item>
/// </list>
/// </summary>
public sealed class UniPlusOperationTransformer : IOpenApiOperationTransformer
{
    private const string ProblemJsonMediaType = "application/problem+json";

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        CoerceErrorResponsesToProblemJson(operation);

        IList<object> metadata = context.Description.ActionDescriptor.EndpointMetadata;

        bool requiresIdempotency = metadata.OfType<RequiresIdempotencyKeyAttribute>().Any();
        if (requiresIdempotency)
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "Idempotency-Key",
                In = ParameterLocation.Header,
                Required = true,
                Description = "Chave opaca (1-255 ASCII printable, sem ',' ou ';') para retry seguro do comando. "
                    + "Replay com mesma key + mesmo body retorna response cacheada (ADR-0027).",
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    MinLength = 1,
                    MaxLength = 255,
                    // ECMA-262 (JSON Schema) — espelha IdempotencyFilter.IsKeyValid:
                    // ASCII printable (0x21-0x7E) menos ',' (0x2C) e ';' (0x3B),
                    // que são separadores em sf-list (draft-ietf-httpapi-idempotency-key).
                    // Mantém o contrato consistente com a validação de runtime — clientes
                    // gerados a partir do spec recebem 400 imediato em chave inválida em vez
                    // de aceitar a chamada e ser rejeitado pelo filter.
                    Pattern = @"^[\x21-\x2B\x2D-\x3A\x3C-\x7E]+$",
                },
            });

            operation.Extensions ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);
            operation.Extensions["x-uniplus-idempotent"] = new JsonNodeExtension(JsonValue.Create(true));
        }

        return Task.CompletedTask;
    }

    private static void CoerceErrorResponsesToProblemJson(OpenApiOperation operation)
    {
        if (operation.Responses is null)
            return;

        foreach (KeyValuePair<string, IOpenApiResponse> kvp in operation.Responses)
        {
            if (!IsErrorStatusKey(kvp.Key))
                continue;

            if (kvp.Value is not OpenApiResponse response)
                continue;

            if (response.Content is null || response.Content.Count == 0)
            {
                response.Content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
                {
                    [ProblemJsonMediaType] = new OpenApiMediaType(),
                };
                continue;
            }

            if (response.Content.ContainsKey(ProblemJsonMediaType))
                continue;

            // Espelha o conteúdo declarado (ex.: "application/json" com schema
            // ProblemDetails) para a media type RFC 9457. Mantém o schema
            // declarado e descarta os outros media types — endpoints de erro
            // só falam problem+json no contrato canônico. Prefere
            // application/json explicitamente; sem ele, cai para o primeiro
            // média type (Dictionary não garante ordem de enumeração).
            OpenApiMediaType source = response.Content.TryGetValue("application/json", out OpenApiMediaType? jsonMediaType)
                ? jsonMediaType
                : response.Content.Values.First();
            response.Content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
            {
                [ProblemJsonMediaType] = source,
            };
        }
    }

    private static bool IsErrorStatusKey(string key)
    {
        if (key.Length != 3)
            return false;

        char first = key[0];
        return first == '4' || first == '5';
    }
}
