namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Declara no documento <b>como</b> se autentica na API: um esquema HTTP Bearer com token JWT,
/// referenciado pelo nome <see cref="SchemeName"/>.
/// </summary>
/// <remarks>
/// <para>
/// O contrato já dizia que uma rota protegida responde <c>401</c> e <c>403</c>
/// (<see cref="AuthorizationOperationTransformer"/>), mas não dizia com que credencial o cliente
/// evita esses status. A lacuna aparece nas duas pontas: uma UI de exploração não tem onde receber
/// o token — e, sem ele, quase toda rota administrativa é inalcançável —, e um cliente gerado a
/// partir do documento nasce sem o cabeçalho de autenticação.
/// </para>
/// <para>
/// <b>Bearer, e não OAuth2 com fluxo declarado.</b> Um fluxo <c>password</c>/<c>authorizationCode</c>
/// precisaria fixar no documento a URL de token do provedor, que muda por ambiente — e, em
/// desenvolvimento, aponta para um host que só existe dentro da rede do Compose
/// (<c>http://keycloak:8080</c>), inalcançável pelo navegador que renderiza a UI. O esquema Bearer
/// descreve o que o servidor de fato exige (um JWT no <c>Authorization</c>) e vale igual em
/// qualquer ambiente; de onde veio o token é assunto de quem chama.
/// </para>
/// </remarks>
public sealed class BearerSecuritySchemeDocumentTransformer : IOpenApiDocumentTransformer
{
    /// <summary>
    /// Nome do esquema em <c>components.securitySchemes</c>, e a chave que cada operação protegida
    /// referencia. Público porque <see cref="AuthorizationOperationTransformer"/> monta o requisito
    /// por operação a partir dele — os dois lados citam a mesma constante, nunca duas literais.
    /// </summary>
    public const string SchemeName = "bearerAuth";

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);

        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",

            // Descrição é conteúdo user-facing do contrato — pt-BR, como a dos demais transformers.
            Description =
                "Token JWT emitido pelo provedor de identidade institucional (Keycloak), enviado no "
                + "cabeçalho Authorization. As rotas administrativas exigem, além da autenticação, a "
                + "role plataforma-admin no token.",
        };

        return Task.CompletedTask;
    }
}
