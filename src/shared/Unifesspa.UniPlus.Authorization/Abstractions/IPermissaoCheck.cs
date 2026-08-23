namespace Unifesspa.UniPlus.Authorization.Abstractions;

using Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>
/// Verificação de contexto declarada por uma permissão do catálogo (ADR-0078,
/// ADR-0080). Cada verificação é uma unidade testável, não código espalhado: o
/// serviço de decisão orquestra a sequência que a permissão declara em
/// <see cref="PermissionRequirement.VerificacoesDeContexto"/>.
/// </summary>
public interface IPermissaoCheck
{
    /// <summary>
    /// Nome da verificação, idêntico ao declarado em <c>decision_checks</c> no
    /// catálogo de permissões. É por este nome que o serviço de decisão liga a
    /// declaração à implementação.
    /// </summary>
    string Nome { get; }

    /// <summary>Executa a verificação sobre o sujeito, o recurso e o contexto da requisição.</summary>
    Task<CheckResult> CheckAsync(
        AuthorizationSubject subject,
        PermissionRequirement requirement,
        ResourceContext resource,
        AuthorizationRequestContext request,
        CancellationToken cancellationToken);
}
