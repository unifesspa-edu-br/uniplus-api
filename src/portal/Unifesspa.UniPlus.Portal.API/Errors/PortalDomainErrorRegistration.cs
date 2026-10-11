namespace Unifesspa.UniPlus.Portal.API.Errors;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Portal.Application.Certames;

/// <summary>
/// Mapeia os erros da Portal para <c>ProblemDetails</c> (ADR-0023 / ADR-0024).
/// </summary>
/// <remarks>
/// A indisponibilidade de Seleção é 503, e não 502 nem 500: é estado transitório da origem, que
/// a borda acompanha com a indicação de quando reencaminhar, e a Portal não o encobre com
/// conteúdo vencido (ADR-0131).
/// </remarks>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IDomainErrorRegistration, PortalDomainErrorRegistration>().")]
internal sealed class PortalDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
    [
        new(PaginaDoCertameErrorCodes.CertameNaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.portal.certame.nao_encontrado",
                "Certame não encontrado")),

        new(PaginaDoCertameErrorCodes.OrigemIndisponivel,
            new DomainErrorMapping(
                StatusCodes.Status503ServiceUnavailable,
                "uniplus.portal.origem_indisponivel",
                "Dados do certame temporariamente indisponíveis")),
    ];
}
