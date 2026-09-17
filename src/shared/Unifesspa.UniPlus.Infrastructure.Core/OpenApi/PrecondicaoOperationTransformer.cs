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
    // 304 entra porque a resposta condicional devolve o selo do recurso: é por ele que o cliente
    // confirma qual representação continua válida, e sem o header declarado o cliente gerado não
    // sabe de onde tirar o valor a repetir no pedido seguinte.
    private static readonly string[] StatusQueCarregamETag = ["200", "201", "204", "304"];

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

        if (metodo.GetCustomAttribute<EmiteETagAttribute>(inherit: true) is { } emiteETag)
        {
            DeclararETagNasRespostas(operation, emiteETag.Descricao ?? DescricaoDoETagDaSessaoEditorial);
        }

        DeclararHeadersProprios(operation, metodo.GetCustomAttributes<EmiteHeaderAttribute>(inherit: true));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Headers de resposta que só um endpoint emite. O ASP.NET não os infere pelo mesmo motivo do
    /// <c>ETag</c> — são escritos em <c>Response.Headers</c>, que é código, não contrato —, e sem
    /// declará-los o cliente gerado não os enxerga.
    /// </summary>
    private static void DeclararHeadersProprios(
        OpenApiOperation operation,
        IEnumerable<EmiteHeaderAttribute> headers)
    {
        foreach (EmiteHeaderAttribute header in headers)
        {
            if (operation.Responses is null
                || !operation.Responses.TryGetValue(header.Status, out IOpenApiResponse? resposta)
                || resposta is not OpenApiResponse concreta)
            {
                continue;
            }

            concreta.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal);
            concreta.Headers[header.Nome] = new OpenApiHeader
            {
                Description = header.Descricao,
                Schema = new OpenApiSchema
                {
                    Type = header.Inteiro ? JsonSchemaType.Integer : JsonSchemaType.String,
                    Format = header.Inteiro ? "int32" : null,
                },
            };
        }
    }

    /// <summary>
    /// Texto padrão do <c>ETag</c>: o da sessão editorial de retificação, que é de onde a maioria
    /// dos emissores vem. Quem emite selo com outro papel — uma leitura pública, que se revalida
    /// com <c>If-None-Match</c> e não abre precondição de mutação alguma — declara o seu próprio em
    /// <see cref="EmiteETagAttribute.Descricao"/>, porque repetir este aqui diria ao integrador
    /// para devolver o selo num <c>If-Match</c> que a rota nem aceita.
    /// </summary>
    private const string DescricaoDoETagDaSessaoEditorial =
        "ETag forte da sessão editorial de retificação, no formato \"{idDaSessao}:{revisao}\". "
        + "Devolva-o no If-Match da próxima mutação. Toda mutação aceita INCREMENTA a revisão e emite o "
        + "tag novo aqui — o cliente encadeia sem um GET no meio. Ausente quando não há sessão em curso "
        + "(o processo em rascunho não tem precondição a satisfazer).";

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

    private static void DeclararETagNasRespostas(OpenApiOperation operation, string descricao)
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
                Description = descricao,
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
public sealed class EmiteETagAttribute : Attribute
{
    /// <summary>
    /// O que o selo significa NESTA rota. Nulo usa o texto da sessão editorial de retificação, que
    /// é o papel da maioria dos emissores — uma leitura pública, que revalida com
    /// <c>If-None-Match</c>, declara o seu.
    /// </summary>
    public string? Descricao { get; init; }
}

/// <summary>
/// A resposta carrega um header próprio do endpoint — o contrato precisa declará-lo, ou o cliente
/// gerado não o enxerga.
/// </summary>
/// <param name="nome">Nome do header, como o endpoint o escreve.</param>
/// <param name="descricao">O que ele carrega.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class EmiteHeaderAttribute(string nome, string descricao) : Attribute
{
    /// <summary>Nome do header.</summary>
    public string Nome { get; } = nome;

    /// <summary>O que o header carrega.</summary>
    public string Descricao { get; } = descricao;

    /// <summary>Status em que ele é emitido. <c>200</c> por padrão.</summary>
    public string Status { get; init; } = "200";

    /// <summary>O valor é um inteiro, e não texto.</summary>
    public bool Inteiro { get; init; }
}
