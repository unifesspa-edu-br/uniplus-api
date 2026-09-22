namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using Errors;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Declara no schema <c>ProblemDetails</c> as propriedades que a resposta carrega
/// por <c>ProblemDetails.Extensions</c>.
/// <para>
/// Um schema transformer não alcança essas propriedades: elas não têm
/// <c>JsonPropertyInfo</c> nomeado — chegam pelo dicionário de extensões, que o
/// gerador descreve como <c>additionalProperties</c> e não como membro. Daí a
/// declaração ser de documento, sobre o schema já emitido.
/// </para>
/// <para>
/// Hoje cobre <c>retryable</c>, que a ADR-0134 usa para distinguir o conflito que
/// vale repetir daquele que só muda de estado por outra ação. Sem a declaração o
/// campo sai na resposta e não existe no contrato: quem gera cliente tipado a
/// partir do spec não tem propriedade para consultar, que é o oposto do que a
/// decisão promete.
/// </para>
/// </summary>
public sealed class ProblemDetailsExtensionsDocumentTransformer : IOpenApiDocumentTransformer
{
    private const string ProblemDetailsSchemaName = "ProblemDetails";
    private const string RetryablePropertyName = ResultExtensions.RetryableExtensionName;

    private const string RetryableDescription =
        "Presente e `true` quando o conflito descreve uma corrida que já passou: a mesma "
        + "requisição, repetida sem alteração, pode agora ser aceita. A ausência é o default "
        + "conservador do servidor — diz que o conflito não foi declarado retentável, não que "
        + "repetir esteja descartado; nesse caso vale a descrição da própria resposta.";

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);

        if (document.Components?.Schemas?.TryGetValue(ProblemDetailsSchemaName, out IOpenApiSchema? schema) != true
            || schema is not OpenApiSchema problemDetails)
        {
            return Task.CompletedTask;
        }

        problemDetails.Properties ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);

        // A propriedade só viaja quando é `true` (ResultExtensions grava a extensão
        // apenas nesse caso), então fica fora de `required`: cliente que a espera
        // sempre presente leria a ausência como erro de contrato.
        problemDetails.Properties[RetryablePropertyName] = new OpenApiSchema
        {
            Type = JsonSchemaType.Boolean,
            Description = RetryableDescription,
        };

        return Task.CompletedTask;
    }
}
