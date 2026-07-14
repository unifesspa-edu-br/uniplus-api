namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using System.Reflection;

using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Idempotency;

/// <summary>
/// Declara no contrato as respostas que o <see cref="IdempotencyFilter{TDbContext}"/> produz
/// — em <b>toda</b> rota marcada com <see cref="RequiresIdempotencyKeyAttribute"/>.
/// </summary>
/// <remarks>
/// <para>
/// O filtro é instalado por <b>convenção</b> (<c>IdempotencyControllerConvention</c>), não por
/// código do controller — e é justamente por isso que o contrato o ignorava: o autor da action
/// declara os status que <b>o handler dele</b> devolve, e não sabe (nem deveria precisar
/// saber) que um resource filter à sua frente responde 400, 409 e 413 por conta própria.
/// </para>
/// <para>
/// O resultado era um contrato que mentia por omissão, e de forma <b>desigual</b>: a maioria
/// das actions declarava o 400, algumas não; o 409 só aparecia onde havia um 409 de
/// <i>domínio</i>; e o <b>413 não era declarado em lugar nenhum do repositório</b>. Um cliente
/// gerado tratava como inesperada uma resposta que o servidor emite por desenho.
/// </para>
/// <para>
/// Declarar isto aqui, e não em ~40 atributos copiados pelos seis módulos, é o que mantém a
/// declaração <b>presa ao mecanismo que a causa</b>: quem acrescentar um endpoint idempotente
/// amanhã herda o contrato correto sem ter de saber disso.
/// </para>
/// </remarks>
public sealed class IdempotenciaOperationTransformer : IOpenApiOperationTransformer
{
    /// <summary>
    /// O que o filtro responde, e por quê. Um status que a action <b>já declara</b> não é
    /// tocado — ela pode ter um motivo próprio para o mesmo código (o 409 de
    /// <c>RascunhoRetificacao.JaAberta</c>, por exemplo), e a descrição dela é a mais
    /// específica.
    /// </summary>
    private static readonly (string Status, string Descricao)[] RespostasDoFiltro =
    [
        ("400",
            "Idempotency-Key ausente ou malformada (uniplus.idempotency.key_ausente, "
            + "uniplus.idempotency.key_malformada), ou corpo JSON inválido."),
        ("409",
            "Requisição concorrente com a mesma Idempotency-Key ainda em processamento "
            + "(uniplus.idempotency.processing_conflict). Repetir depois — a operação anterior "
            + "ainda não concluiu."),
        ("413",
            "Corpo acima do limite dos endpoints idempotentes (uniplus.idempotency.body_muito_grande). "
            + "O limite é do filtro, não do servidor."),
        ("422",
            "Mesma Idempotency-Key reusada com corpo diferente (uniplus.idempotency.body_mismatch) — "
            + "a chave identifica a requisição pelo hash do corpo."),
    ];

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        if (context.Description.ActionDescriptor is not ControllerActionDescriptor descriptor)
        {
            return Task.CompletedTask;
        }

        // Method-level vence class-level — a mesma resolução que o filtro faz.
        bool idempotente =
            descriptor.MethodInfo.GetCustomAttribute<RequiresIdempotencyKeyAttribute>(inherit: true) is not null
            || descriptor.ControllerTypeInfo.GetCustomAttribute<RequiresIdempotencyKeyAttribute>(inherit: true) is not null;

        if (!idempotente)
        {
            return Task.CompletedTask;
        }

        operation.Responses ??= [];

        foreach ((string status, string descricao) in RespostasDoFiltro)
        {
            // Não sobrescreve o que a action já declarou: ela conhece a causa dela, e a
            // descrição específica vale mais que a genérica do filtro.
            if (operation.Responses.ContainsKey(status))
            {
                continue;
            }

            operation.Responses[status] = new OpenApiResponse { Description = descricao };
        }

        return Task.CompletedTask;
    }
}
