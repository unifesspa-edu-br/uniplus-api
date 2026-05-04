namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

public static class DomainErrorMappingServiceCollectionExtensions
{
    public static IServiceCollection AddDomainErrorMapper(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IDomainErrorRegistration, KernelDomainErrorRegistration>();
        services.AddSingleton<IDomainErrorMapper>(sp =>
        {
            IEnumerable<IDomainErrorRegistration> registrations = sp.GetServices<IDomainErrorRegistration>();
            return new DomainErrorMappingRegistry(registrations);
        });
        return services;
    }
}
