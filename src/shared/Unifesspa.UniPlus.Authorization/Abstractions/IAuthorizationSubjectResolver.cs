namespace Unifesspa.UniPlus.Authorization.Abstractions;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Agregador do sujeito na <b>borda</b> (ADR-0078): monta o
/// <see cref="AuthorizationSubject"/> a partir da identidade autenticada, para
/// então entregá-lo pronto ao ponto de decisão. O sujeito nunca é construído
/// dentro do serviço de decisão nem inferido do corpo da requisição — o ator
/// deriva de <see cref="IUserContext"/> (ADR-0033).
/// </summary>
public interface IAuthorizationSubjectResolver
{
    /// <summary>
    /// Resolve o sujeito da requisição corrente.
    /// </summary>
    /// <remarks>
    /// A falha é de <b>autenticação</b>, não de autorização: sem emissor,
    /// subject ou <c>jti</c> não há sujeito sobre o qual decidir, e a borda
    /// responde <c>401</c> — jamais <c>403</c>, que afirmaria uma decisão de
    /// acesso que não chegou a ser tomada. Os códigos estáveis da falha estão em
    /// <see cref="Errors.AuthorizationErrorCodes"/>.
    /// </remarks>
    Task<Result<AuthorizationSubject>> ResolveAsync(
        IUserContext userContext,
        AuthorizationRequestContext request,
        CancellationToken cancellationToken);
}
