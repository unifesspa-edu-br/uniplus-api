namespace Unifesspa.UniPlus.Authorization.Abstractions;

using Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>
/// Ponto de decisão único de autorização (ADR-0078). Toda decisão de acesso do
/// Uni+ passa por aqui, com o <b>sujeito explícito</b> na assinatura — nunca
/// inferido de estado ambiental. Regra de negócio de autorização vive neste
/// serviço, não em <c>[Authorize(Policy = …)]</c>, que fica restrito à
/// autenticação básica.
/// </summary>
public interface IAuthorizationDecisionService
{
    /// <summary>
    /// Decide o acesso do <paramref name="subject"/> à permissão exigida por
    /// <paramref name="requirement"/> sobre <paramref name="resource"/>, no
    /// contexto de <paramref name="request"/>.
    /// </summary>
    /// <remarks>
    /// Não lança por decisão negativa: a negativa é um <see cref="AuthorizationDecision"/>
    /// com <see cref="AuthorizationDecision.DenyReason"/> preenchido. Lança apenas
    /// diante de violação de contrato de programação (ex.: requisito exigindo uma
    /// verificação de contexto que o composition root não registrou).
    /// </remarks>
    Task<AuthorizationDecision> DecideAsync(
        AuthorizationSubject subject,
        PermissionRequirement requirement,
        ResourceContext resource,
        AuthorizationRequestContext request,
        CancellationToken cancellationToken);
}
