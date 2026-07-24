namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Declara no contrato as respostas de <b>autorização</b> — <c>401</c> e <c>403</c> — em toda
/// operação de controller protegida por <see cref="AuthorizeAttribute"/> (na action ou na classe)
/// e não liberada por <see cref="AllowAnonymousAttribute"/>.
/// </summary>
/// <remarks>
/// <para>
/// A proteção é declarada uma vez, no nível da classe (<c>[Authorize(Roles = "…")]</c>), e o autor
/// de cada action não repete os status que a política produz — do mesmo jeito que ninguém declara,
/// action a action, o 413 que o filtro de idempotência devolve. O resultado era um contrato que
/// omitia o <c>401</c> (sem token) e o <c>403</c> (autenticado sem a role) na maioria das
/// operações, e os declarava à mão só em algumas — um cliente gerado tratava como inesperada uma
/// resposta que o servidor emite por desenho.
/// </para>
/// <para>
/// Declarar isto aqui, preso ao mecanismo que o causa (a presença de <c>[Authorize]</c>), e não em
/// dezenas de atributos copiados pelos módulos, é o que faz um endpoint protegido novo herdar o
/// contrato correto sem o autor ter de saber disso. Uma action que já declara o próprio 401/403 —
/// com uma descrição mais específica — não é sobrescrita.
/// </para>
/// </remarks>
public sealed class AuthorizationOperationTransformer : IOpenApiOperationTransformer
{
    // As descrições são conteúdo user-facing do contrato (lidas por quem consome a API) — pt-BR,
    // como as dos demais transformers. Os identificadores de código são em inglês (camada de infra).
    private static readonly (string Status, string Description)[] AuthorizationResponses =
    [
        ("401",
            "Requisição não autenticada — token ausente ou inválido (a rota exige autenticação). "
            + "Também emitido quando o principal é exigido e não está presente "
            + "(uniplus.idempotency.principal_requerido)."),
        ("403",
            "Autenticado, mas sem a autorização exigida pela rota (ex.: a role plataforma-admin)."),
    ];

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        // Scope: controllers only. Minimal API endpoints have their own lifecycle and are not the
        // target of this controller-route contract fix.
        if (context.Description.ActionDescriptor is not ControllerActionDescriptor descriptor)
        {
            return Task.CompletedTask;
        }

        // Reads the endpoint metadata (action AND class attributes, aggregated by MVC) through the
        // SAME interfaces the authorization middleware consults — IAllowAnonymous wins over
        // IAuthorizeData. So the contract describes exactly what the server does, and the test does
        // not need controller fixtures.
        IList<object> metadata = descriptor.EndpointMetadata;

        bool anonymous = metadata.OfType<IAllowAnonymous>().Any();
        if (anonymous)
        {
            return Task.CompletedTask;
        }

        bool requiresAuthorization = metadata.OfType<IAuthorizeData>().Any();
        if (!requiresAuthorization)
        {
            return Task.CompletedTask;
        }

        operation.Responses ??= [];

        foreach ((string status, string description) in AuthorizationResponses)
        {
            // An action that already declares its own 401/403 knows its cause — do not overwrite.
            if (operation.Responses.ContainsKey(status))
            {
                continue;
            }

            // The server always emits an application/problem+json body (RFC 9457) for these
            // statuses — the same ProblemDetails the hand-declared 401/403 model. Referencing the
            // schema (not a description-only response) is what makes a generated client treat the
            // error as typed. ProblemDetails lives in components because other error responses use it.
            operation.Responses[status] = new OpenApiResponse
            {
                Description = description,
                Content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
                {
                    ["application/problem+json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchemaReference("ProblemDetails"),
                    },
                },
            };
        }

        return Task.CompletedTask;
    }
}
