namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using System.Reflection;

using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Declara no OpenAPI os headers de resposta que um endpoint emite: o <c>ETag</c> e os próprios de cada rota.
/// </summary>
/// <remarks>
/// O ASP.NET não infere nada disso: o <c>ETag</c> que o servidor escreve em <c>Response.Headers</c> <b>não entra
/// no spec de jeito nenhum</b> — é código, não contrato. Um cliente que só expõe headers declarados não
/// consegue ler o selo, e portanto não consegue revalidar a representação com <c>If-None-Match</c>.
/// </remarks>
public sealed class HeadersDeRespostaOperationTransformer : IOpenApiOperationTransformer
{
    private const string ETagHeader = "ETag";

    // 201 entra porque a criação devolve o selo da primeira representação, que a leitura e a mutação seguintes repetem.
    // 304 entra porque a resposta condicional devolve o selo do recurso: é por ele que o cliente
    // confirma qual representação continua válida, e sem o header declarado o cliente gerado não
    // sabe de onde tirar o valor a repetir no pedido seguinte.
    private static readonly string[] StatusQueCarregamETag = ["200", "201", "304"];

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        if ((context.Description.ActionDescriptor as ControllerActionDescriptor)?.MethodInfo is { } metodo)
        {
            DeclararHeadersDoMetodo(operation, metodo);
        }

        return Task.CompletedTask;
    }

    private static void DeclararHeadersDoMetodo(OpenApiOperation operation, MethodInfo metodo)
    {
        if (metodo.GetCustomAttribute<EmiteETagAttribute>(inherit: true) is { } emiteETag)
        {
            DeclararETagNasRespostas(operation, emiteETag.Descricao);
        }

        DeclararHeadersProprios(operation, metodo.GetCustomAttributes<EmiteHeaderAttribute>(inherit: true));
    }

    /// <summary>
    /// Headers de resposta que só um endpoint emite. O ASP.NET não os infere pelo mesmo motivo do
    /// <c>ETag</c> — são escritos em <c>Response.Headers</c>, que é código, não contrato —, e sem
    /// declará-los o cliente gerado não os enxerga.
    /// </summary>
    private static void DeclararHeadersProprios(OpenApiOperation operation, IEnumerable<EmiteHeaderAttribute> headers)
    {
        foreach (EmiteHeaderAttribute header in headers)
        {
            DeclararHeaderNaResposta(operation, header.Status, header.Nome, new OpenApiHeader
            {
                Description = header.Descricao,
                Schema = new OpenApiSchema
                {
                    Type = header.Inteiro ? JsonSchemaType.Integer : JsonSchemaType.String,
                    Format = header.Inteiro ? "int32" : null,
                },
            });
        }
    }

    private static void DeclararETagNasRespostas(OpenApiOperation operation, string descricao)
    {
        foreach (string status in StatusQueCarregamETag)
        {
            DeclararHeaderNaResposta(operation, status, ETagHeader, new OpenApiHeader
            {
                Description = descricao,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            });
        }
    }

    private static void DeclararHeaderNaResposta(OpenApiOperation operation, string status, string nome, OpenApiHeader header)
    {
        if (operation.Responses is null
            || !operation.Responses.TryGetValue(status, out IOpenApiResponse? resposta)
            || resposta is not OpenApiResponse concreta)
        {
            return;
        }

        concreta.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal);
        concreta.Headers[nome] = header;
    }
}

/// <summary>
/// A resposta carrega o header <c>ETag</c> — o contrato precisa declará-lo, ou o cliente
/// gerado não o enxerga.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class EmiteETagAttribute : Attribute
{
    /// <summary>
    /// O que o selo significa NESTA rota.
    /// </summary>
    public required string Descricao { get; init; }
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
