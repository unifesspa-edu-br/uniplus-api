namespace Unifesspa.UniPlus.Authorization.Abstractions;

/// <summary>
/// Porta pela qual um módulo descobre a quem avisar de uma fila cuja decisão cabe a quem tem uma
/// permissão do catálogo. Quem guarda as concessões implementa a porta; o módulo que avisa só a consome,
/// sem conhecer o armazenamento (ADR-0078).
/// </summary>
public interface ITitularesDaPermissao
{
    /// <summary>
    /// Os e-mails, distintos e em minúsculas, das pessoas com concessão vigente, no escopo da instituição, de um
    /// perfil que reúne a permissão. A concessão sem e-mail fica de fora: sem endereço não há a quem avisar.
    /// </summary>
    /// <param name="codigoDaPermissao">Código <c>{módulo}:{resource}:{ação}</c> da permissão no catálogo.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task<IReadOnlyList<string>> ListarEmailsAsync(string codigoDaPermissao, CancellationToken cancellationToken = default);
}
