namespace Unifesspa.UniPlus.Publicacoes.API.Errors;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Mapeia os erros de domínio do módulo Publicações para <c>ProblemDetails</c>
/// (ADR-0023 / ADR-0024), reunindo os mapeamentos de cada assunto numa ordem só.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IDomainErrorRegistration, PublicacoesDomainErrorRegistration>().")]
internal sealed class PublicacoesDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
    [
        .. ErrosDoTipoDeAto.Mapeamentos,
        .. ErrosDoAtoNormativo.Mapeamentos,
        .. ErrosDoAnexoDoAto.Mapeamentos,
    ];
}
