namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using Formatting;

using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Anuncia, nas respostas de sucesso, as vendor MIME que a operação de fato aceita
/// (<c>application/vnd.uniplus.{recurso}.v{n}+json</c>, ADR-0028) — no lugar dos media types
/// genéricos que o ApiExplorer infere do formatter.
/// </summary>
/// <remarks>
/// <para>
/// A negociação acontece em runtime, no <see cref="VendorMediaTypeAttribute"/>, e o documento não
/// sabia disso: declarava <c>application/json</c>, <c>text/json</c> e <c>text/plain</c>, porque é
/// o que o formatter JSON registra. O descompasso não é cosmético — uma interface de exploração
/// escolhe o primeiro media type declarado e recebe <c>406</c>, e um cliente gerado a partir do
/// contrato nasce mandando um <c>Accept</c> que o servidor recusa. O contrato descrevia uma API
/// que não é esta.
/// </para>
/// <para>
/// Só as respostas <b>2xx</b> são reescritas. Erro é <c>application/problem+json</c> em qualquer
/// versão do recurso (RFC 9457, ADR-0023) — trocar o media type de um <c>404</c> por uma vendor
/// MIME diria que o corpo do erro muda de forma com a versão, o que não acontece.
/// </para>
/// </remarks>
public sealed class VendorMediaTypeOperationTransformer : IOpenApiOperationTransformer
{
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

        // A metadata agrega o atributo posto na action E na classe — a mesma fonte que o MVC
        // consulta para executar o filtro, então contrato e comportamento não podem divergir.
        VendorMediaTypeAttribute? vendor = descriptor.EndpointMetadata
            .OfType<VendorMediaTypeAttribute>()
            .FirstOrDefault();

        if (vendor is null || string.IsNullOrEmpty(vendor.Resource) || vendor.Versions.Length == 0)
        {
            return Task.CompletedTask;
        }

        if (operation.Responses is null)
        {
            return Task.CompletedTask;
        }

        foreach ((string status, IOpenApiResponse resposta) in operation.Responses)
        {
            if (!EhSucesso(status) || resposta is not OpenApiResponse concreta || concreta.Content is null)
            {
                continue;
            }

            // O schema é o mesmo em todas as versões declaradas: o que a vendor MIME versiona é o
            // contrato do recurso, e o documento descreve a versão que ESTE documento publica.
            // Preservá-lo do media type genérico evita reescrever o corpo da resposta aqui.
            IOpenApiSchema? schema = concreta.Content.Values.FirstOrDefault()?.Schema;

            Dictionary<string, OpenApiMediaType> conteudoVersionado = new(StringComparer.Ordinal);
            foreach (int versao in vendor.Versions)
            {
                conteudoVersionado[VendorMediaTypeAttribute.BuildVendorMime(vendor.Resource, versao)] =
                    new OpenApiMediaType { Schema = schema };
            }

            concreta.Content = conteudoVersionado;
        }

        return Task.CompletedTask;
    }

    private static bool EhSucesso(string status) =>
        status.Length == 3 && status[0] == '2';
}
