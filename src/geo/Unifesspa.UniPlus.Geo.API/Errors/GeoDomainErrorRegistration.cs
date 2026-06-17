namespace Unifesspa.UniPlus.Geo.API.Errors;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Registry de mapeamentos de erros de domínio do módulo Geo para wire codes /
/// status HTTP. Vazio em V1 — as entidades de localidade entram nas Stories de
/// domínio/API com seus próprios códigos <c>uniplus.geo.*</c>.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IDomainErrorRegistration, GeoDomainErrorRegistration>().")]
internal sealed class GeoDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() => [];
}
