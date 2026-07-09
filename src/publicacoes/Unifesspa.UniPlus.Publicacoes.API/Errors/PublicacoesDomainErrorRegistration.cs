namespace Unifesspa.UniPlus.Publicacoes.API.Errors;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IDomainErrorRegistration, PublicacoesDomainErrorRegistration>().")]
internal sealed class PublicacoesDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() => [];
}
