namespace Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// A trava transacional por pessoa e a conferência dos papéis incompatíveis (Representante do controlador e Encarregado
/// de dados), que todo caminho que concede ou revoga concessão de perfil de governança usa (ADR-0162).
/// </summary>
/// <remarks>
/// Ordem única das travas, do primeiro ao último recurso: a revisão, o perfil (a trava do papel de Representante é a linha
/// do perfil dele), o pedido, a pessoa, a concessão e, por último, a trava dos gestores. Nenhum caminho trava a pessoa e
/// depois um perfil, um pedido ou uma revisão, e a ordem não fecha ciclo.
/// </remarks>
public interface IIncompatibilidadeDosPapeis
{
    /// <summary>
    /// Trava cada pessoa até o fim da transação, em ordem de emissor e <c>sub</c>, para que duas designações cruzadas da
    /// mesma pessoa não leiam ambas que ela não tem o outro papel.
    /// </summary>
    Task TravarAsync(IEnumerable<Identidade> pessoas, CancellationToken cancellationToken = default);

    /// <summary>
    /// A pessoa tem, no instante, concessão vigente do papel incompatível com o perfil pedido; a vencida ou revogada não
    /// conta, e o perfil sem papel incompatível nunca tem.
    /// </summary>
    Task<bool> TemPapelIncompativelAsync(Identidade pessoa, Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default);
}
