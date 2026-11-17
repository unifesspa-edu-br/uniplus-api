namespace Unifesspa.UniPlus.OrganizacaoInstitucional.API;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.OrganizacaoInstitucional.API.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.API.Hateoas;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Permissoes;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure;

/// <summary>
/// Registro self-describing do módulo OrganizacaoInstitucional para o composition
/// root do monólito modular. Reúne tudo que é específico do módulo: o documento
/// OpenAPI 3.1 (ADR-0030), publicado em <c>/openapi/organizacao.json</c>; os erros de
/// domínio, consumidos por <c>AddDomainErrorMapper</c>, que o host registra uma vez via
/// <c>IEnumerable&lt;IDomainErrorRegistration&gt;</c>; os builders HATEOAS; as permissões (ADR-0151), cujo enum entra no catálogo da
/// aplicação e é conferido na subida; e
/// Application e Infrastructure, que registram validators, repositórios, readers
/// cross-módulo (ADR-0056) e cache invalidators.
///
/// O que é compartilhado entre módulos (Serilog, auth, CORS, cache, storage,
/// observabilidade, middleware, AddControllers, AddDomainErrorMapper, criptografia,
/// cursor pagination, health checks de infra) fica no host. O wiring do Wolverine
/// (consolidado numa única instância) também é responsabilidade do host. Mantém o
/// módulo extraível: um serviço próprio chamaria este mesmo método.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Referenciado pelo composition root (host do monólito modular) fora deste assembly.")]
public static class OrganizacaoInstitucionalModuleRegistration
{
    public static IServiceCollection AddOrganizacaoInstitucionalModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddUniPlusOpenApi(OrganizacaoApiAssemblyMarker.ModuleName, configuration);
        services.AddSingleton<IDomainErrorRegistration, OrganizacaoDomainErrorRegistration>();
        AddHateoas(services);
        services.AddCatalogoDePermissoes<PermissaoOrganizacao>();
        services.AddOrganizacaoInstitucionalApplication();
        services.AddOrganizacaoInstitucionalInfrastructure();

        return services;
    }

    /// <summary>Os builders de <c>_links</c> (HATEOAS Level 1, ADR-0029/0049) dos recursos do módulo.</summary>
    private static void AddHateoas(IServiceCollection services)
    {
        services.AddSingleton<IResourceLinksBuilder<UnidadeDto>, UnidadeLinksBuilder>();
        services.AddSingleton<IResourceLinksBuilder<InstituicaoDto>, InstituicaoLinksBuilder>();
    }
}
