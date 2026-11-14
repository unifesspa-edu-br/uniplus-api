namespace Unifesspa.UniPlus.Publicacoes.API;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Publicacoes.API.Errors;
using Unifesspa.UniPlus.Publicacoes.API.Hateoas;
using Unifesspa.UniPlus.Publicacoes.Application;
using Unifesspa.UniPlus.Publicacoes.Application.DTOs;
using Unifesspa.UniPlus.Publicacoes.Application.Permissoes;
using Unifesspa.UniPlus.Publicacoes.Infrastructure;

/// <summary>
/// Registro self-describing do módulo Publicações para o composition root do
/// monólito modular (ADR-0097). Reúne o que é específico do módulo — OpenAPI
/// doc, registro de erros de domínio, builders HATEOAS, Application e Infrastructure.
///
/// O módulo não referencia Domain, Application, Infrastructure ou API de nenhum
/// outro módulo (ADR-0105): ele possui a essência documental do ato, e nada mais.
///
/// Os handlers vivem no assembly Application e são descobertos pelo Wolverine, que
/// os inclui explicitamente no <c>Discovery</c> do composition root.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Referenciado pelo composition root (host do monólito modular) fora deste assembly.")]
public static class PublicacoesModuleRegistration
{
    public static IServiceCollection AddPublicacoesModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // OpenAPI 3.1 (ADR-0030) — spec em /openapi/publicacoes.json.
        services.AddUniPlusOpenApi(PublicacoesApiAssemblyMarker.ModuleName, configuration);

        // Erros de domínio do módulo (consumidos por AddDomainErrorMapper, registrado
        // uma vez no host via IEnumerable<IDomainErrorRegistration>).
        services.AddSingleton<IDomainErrorRegistration, PublicacoesDomainErrorRegistration>();

        services.AddSingleton<IResourceLinksBuilder<TipoAtoPublicadoDto>, TipoAtoPublicadoLinksBuilder>();
        services.AddSingleton<IResourceLinksBuilder<AtoNormativoDto>, AtoNormativoLinksBuilder>();

        // Permissões do módulo (ADR-0151): o enum entra no catálogo da aplicação e é
        // conferido na subida.
        services.AddCatalogoDePermissoes<PermissaoPublicacoes>();

        services.AddPublicacoesApplication();
        services.AddPublicacoesInfrastructure();

        return services;
    }
}
