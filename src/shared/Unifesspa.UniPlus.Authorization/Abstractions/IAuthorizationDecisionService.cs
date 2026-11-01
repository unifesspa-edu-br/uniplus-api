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
    /// Registra a decisão — permitida ou negada — antes de devolvê-la, mas só quando a permissão ou o resource é de
    /// sensibilidade pessoal, identificadora ou sensível (<see cref="Contracts.RegistroDecisaoAcesso.Exige"/>); as
    /// demais decisões não gravam nada. A falha de gravação lança <see cref="RegistroDeAcessoNaoGravadoException"/> e o
    /// chamador não recebe decisão. Não lança por decisão negativa: a negativa é um <see cref="AuthorizationDecision"/>
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

    /// <summary>
    /// Decide como <see cref="DecideAsync"/>, sem gravar nada, nem a negativa. É o caminho de quem decide em nome do
    /// sistema, como o projetor: a negativa dele não é tentativa de ninguém, e a permitida ele mesmo registra.
    /// </summary>
    Task<AuthorizationDecision> DecidirSemRegistrarAsync(
        AuthorizationSubject subject,
        PermissionRequirement requirement,
        ResourceContext resource,
        AuthorizationRequestContext request,
        CancellationToken cancellationToken);
}
