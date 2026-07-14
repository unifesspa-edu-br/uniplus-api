namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using System.Reflection;

using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Declara no OpenAPI o <b>protocolo de concorrência otimista</b> — o <c>ETag</c> que a
/// resposta emite, e a obrigatoriedade do <c>If-Match</c> onde ela é incondicional.
/// </summary>
/// <remarks>
/// <para>
/// O ASP.NET não infere nada disso. O <c>If-Match</c> declarado como
/// <c>[FromHeader] string?</c> entra no spec como parâmetro <b>opcional</b>, e o
/// <c>ETag</c> que o servidor escreve em <c>Response.Headers</c> <b>não entra de jeito
/// nenhum</b> — é código, não contrato.
/// </para>
/// <para>
/// A consequência é concreta e o gerador de cliente a torna inescapável: um cliente que só
/// expõe headers declarados <b>não consegue ler o ETag</b>, e portanto não consegue
/// encadear o <c>If-Match</c> da mutação seguinte sem um <c>GET</c> extra — quando o
/// servidor acabou de lhe dar o tag de graça. E um <c>If-Match</c> opcional numa rota que
/// <b>não funciona sem ele</b> convida o cliente a omiti-lo e levar 428, sem que nada no
/// contrato o avisasse.
/// </para>
/// </remarks>
public sealed class PrecondicaoOperationTransformer : IOpenApiOperationTransformer
{
    private const string IfMatchParam = "If-Match";
    private const string ETagHeader = "ETag";

    /// <summary>
    /// Onde o <c>ETag</c> é emitido: na criação da sessão (201), na leitura dela (200) e em
    /// <b>toda mutação aceita</b> (204) — esta última é a que importa, porque é o único
    /// lugar em que o cliente recebe a precondição da chamada seguinte.
    /// </summary>
    private static readonly string[] StatusQueCarregamETag = ["200", "201", "204"];

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        MethodInfo? metodo = (context.Description.ActionDescriptor as ControllerActionDescriptor)?.MethodInfo;
        if (metodo is null)
        {
            return Task.CompletedTask;
        }

        if (metodo.GetCustomAttribute<PrecondicaoObrigatoriaAttribute>(inherit: true) is not null)
        {
            MarcarIfMatchComoObrigatorio(operation);
        }

        if (metodo.GetCustomAttribute<EmiteETagAttribute>(inherit: true) is not null)
        {
            DeclararETagNasRespostas(operation);
        }

        return Task.CompletedTask;
    }

    private static void MarcarIfMatchComoObrigatorio(OpenApiOperation operation)
    {
        if (operation.Parameters is null)
        {
            return;
        }

        IEnumerable<OpenApiParameter> ifMatch = operation.Parameters
            .OfType<OpenApiParameter>()
            .Where(static p => p.In == ParameterLocation.Header
                && string.Equals(p.Name, IfMatchParam, StringComparison.OrdinalIgnoreCase));

        foreach (OpenApiParameter parametro in ifMatch)
        {
            parametro.Required = true;
            parametro.Description = "Precondição de concorrência (RFC 9110 §13.1.1) — o ETag da sessão editorial "
                + "em curso. OBRIGATÓRIO nesta rota: ela existe para a sessão, e sem a precondição responde 428. "
                + "Comparação FORTE: uma weak tag (W/\"...\") nunca casa, e produz 412.";
        }
    }

    private static void DeclararETagNasRespostas(OpenApiOperation operation)
    {
        if (operation.Responses is null)
        {
            return;
        }

        foreach (string status in StatusQueCarregamETag)
        {
            if (!operation.Responses.TryGetValue(status, out IOpenApiResponse? resposta)
                || resposta is not OpenApiResponse concreta)
            {
                continue;
            }

            concreta.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal);
            concreta.Headers[ETagHeader] = new OpenApiHeader
            {
                Description = "ETag forte da sessão editorial de retificação, no formato \"{idDaSessao}:{revisao}\". "
                    + "Devolva-o no If-Match da próxima mutação. Toda mutação aceita INCREMENTA a revisão e emite o "
                    + "tag novo aqui — o cliente encadeia sem um GET no meio. Ausente quando não há sessão em curso "
                    + "(o processo em rascunho não tem precondição a satisfazer).",
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                },
            };
        }
    }
}

/// <summary>
/// A rota <b>não funciona sem</b> <c>If-Match</c> — ela existe para a sessão editorial, e a
/// ausência da precondição é falha de protocolo (428), não estado válido.
/// </summary>
/// <remarks>
/// Não confundir com as rotas de <b>obrigatoriedade condicional</b>: os <c>Definir*</c>
/// servem também um recurso sem sessão aberta, onde não há ETag a fornecer, e para elas o
/// header é legitimamente opcional (ADR-0110 D5).
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PrecondicaoObrigatoriaAttribute : Attribute;

/// <summary>
/// A resposta carrega o header <c>ETag</c> — o contrato precisa declará-lo, ou o cliente
/// gerado não o enxerga.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class EmiteETagAttribute : Attribute;
