namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Declara no contrato a recusa por corpo acima do limite (<c>uniplus.requisicao.corpo_acima_do_limite</c>) em toda operação
/// que recebe corpo.
/// </summary>
/// <remarks>
/// A recusa nasce no servidor, antes da action, então o autor da action não a declara. Cada operação tem o seu limite: o
/// geral das opções ou o do <c>[RequestSizeLimit]</c> da própria action.
/// </remarks>
public sealed class CorpoAcimaDoLimiteOperationTransformer : IOpenApiOperationTransformer
{
    private const string Status = "413";

    private const string Descricao =
        "O corpo da requisição passa do limite de tamanho aceito pela operação (uniplus.requisicao.corpo_acima_do_limite). "
        + "Com o tamanho declarado acima do limite, a recusa vem antes de verificar o acesso e de ler o conteúdo. "
        + "Sem tamanho declarado, com o conteúdo enviado em partes, a leitura é interrompida ao passar do limite, "
        + "e a verificação de acesso pode recusar antes disso.";

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        if (operation.RequestBody is not null)
        {
            operation.Responses ??= [];
            operation.Responses.TryAdd(Status, RecusaPorCorpoAcimaDoLimite());
        }

        return Task.CompletedTask;
    }

    private static OpenApiResponse RecusaPorCorpoAcimaDoLimite() => new()
    {
        Description = Descricao,
        Content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
        {
            ["application/problem+json"] = new OpenApiMediaType { Schema = new OpenApiSchemaReference("ProblemDetails") },
        },
    };
}
