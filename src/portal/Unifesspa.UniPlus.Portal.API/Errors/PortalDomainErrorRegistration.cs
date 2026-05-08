namespace Unifesspa.UniPlus.Portal.API.Errors;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IDomainErrorRegistration, PortalDomainErrorRegistration>().")]
internal sealed class PortalDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() => [];
}
