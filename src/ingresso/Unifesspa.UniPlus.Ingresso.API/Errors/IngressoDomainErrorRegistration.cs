namespace Unifesspa.UniPlus.Ingresso.API.Errors;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IDomainErrorRegistration, IngressoDomainErrorRegistration>().")]
internal sealed class IngressoDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() => [];
}
