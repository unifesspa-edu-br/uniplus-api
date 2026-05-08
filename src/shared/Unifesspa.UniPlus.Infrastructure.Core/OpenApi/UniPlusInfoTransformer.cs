namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

/// <summary>
/// Document transformer que injeta metadata institucional Uni+ em todos os
/// documentos OpenAPI gerados (selecao, ingresso, futuros). Aplica info
/// (title, description, contact, license em pt-BR), servers por ambiente, e
/// versão de contrato alinhada com ADR-0022 (Contrato REST canônico V1).
/// </summary>
public sealed class UniPlusInfoTransformer : IOpenApiDocumentTransformer
{
    private readonly UniPlusOpenApiOptions _options;

    public UniPlusInfoTransformer(IOptions<UniPlusOpenApiOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);

        string moduleTitle = context.DocumentName switch
        {
            "selecao" => "Uni+ — Módulo Seleção",
            "ingresso" => "Uni+ — Módulo Ingresso",
            "portal" => "Uni+ — Módulo Portal",
            _ => $"Uni+ — {context.DocumentName}",
        };

        document.Info = new OpenApiInfo
        {
            Title = moduleTitle,
            Version = _options.ContractVersion,
            Description = "API REST do Sistema Unificado Unifesspa (Uni+). Contrato canônico V1 — "
                + "ProblemDetails RFC 9457, paginação cursor opaco, vendor MIME versioning. "
                + "Toda string user-facing em pt-BR (ADR-0022).",
            Contact = new OpenApiContact
            {
                Name = _options.ContactName,
                Email = _options.ContactEmail,
                Url = new Uri(_options.ContactUrl),
            },
            License = new OpenApiLicense
            {
                Name = "MIT",
                Url = new Uri("https://opensource.org/licenses/MIT"),
            },
        };

        document.Servers =
        [
            new OpenApiServer { Url = _options.ProductionServerUrl, Description = "Produção" },
            new OpenApiServer { Url = _options.StagingServerUrl, Description = "Homologação" },
        ];

        return Task.CompletedTask;
    }
}
