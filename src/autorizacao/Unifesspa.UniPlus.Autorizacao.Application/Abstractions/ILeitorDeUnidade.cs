namespace Unifesspa.UniPlus.Autorizacao.Application.Abstractions;

using Unifesspa.UniPlus.Governance.Contracts;

/// <summary>
/// Lê a unidade da Organização Institucional. É uma porta própria, registrada por fábrica, para que o handler do
/// pedido continue enxergando um único <c>DbContext</c> e grave o pedido e os avisos na mesma transação.
/// </summary>
public interface ILeitorDeUnidade
{
    Task<UnidadeView?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
}
