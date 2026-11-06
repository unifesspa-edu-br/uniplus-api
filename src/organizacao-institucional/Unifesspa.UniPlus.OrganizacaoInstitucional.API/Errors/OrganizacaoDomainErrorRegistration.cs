namespace Unifesspa.UniPlus.OrganizacaoInstitucional.API.Errors;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IDomainErrorRegistration, OrganizacaoDomainErrorRegistration>().")]
internal sealed class OrganizacaoDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
    [
        .. UnidadeErrorMappings.Todos,
        .. InstituicaoErrorMappings.Todos,
    ];
}
