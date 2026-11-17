namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;

/// <summary>
/// Registra recursos da camada Application do módulo OrganizacaoInstitucional.
/// CQRS roda integralmente sobre Wolverine (<c>ICommandBus</c>/<c>IQueryBus</c>),
/// então esta extensão registra os validators FluentValidation, consumidos pelo
/// middleware de validação FluentValidation do Wolverine (<c>UseFluentValidation</c>,
/// configurado em Infrastructure.Core), e os serviços de conferência e gravação da Instituição.
/// </summary>
public static class OrganizacaoInstitucionalApplicationServiceRegistration
{
    public static IServiceCollection AddOrganizacaoInstitucionalApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        System.Reflection.Assembly assembly = typeof(OrganizacaoInstitucionalApplicationServiceRegistration).Assembly;
        services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<ConferenciaDoCadastroDaInstituicao>();
        services.AddScoped<GravacaoDaInstituicao>();
        services.AddScoped<ConferenciaDoCadastroDaUnidade>();
        services.AddScoped<GravacaoDaUnidade>();

        return services;
    }
}
