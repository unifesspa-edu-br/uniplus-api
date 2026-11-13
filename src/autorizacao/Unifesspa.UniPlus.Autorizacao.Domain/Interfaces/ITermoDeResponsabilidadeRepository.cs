namespace Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// O registro da vigência do termo de responsabilidade, as versões publicadas e a exceção da instalação. A linha do
/// registro é a trava da vigência na ordem única da ADR-0162: exclusiva na publicação, compartilhada na criação de pedido
/// protegido e na instalação.
/// </summary>
public interface ITermoDeResponsabilidadeRepository
{
    /// <summary>
    /// Trava o registro com <c>FOR SHARE</c> até o fim da transação e devolve a versão vigente, nula se nenhuma foi
    /// publicada. A publicação concorrente espera esta transação; a que já travou o registro é esperada e relida.
    /// </summary>
    Task<VersaoDoTermo?> ObterVigenteSobTravaCompartilhadaAsync(CancellationToken cancellationToken = default);

    /// <summary>Trava o registro com <c>FOR UPDATE</c> até o fim da transação e o devolve rastreado, com a vigente.</summary>
    Task<(TermoDeResponsabilidade Termo, VersaoDoTermo? Vigente)> ObterParaPublicarAsync(CancellationToken cancellationToken = default);

    /// <summary>A versão vigente, sem trava, para a consulta.</summary>
    Task<VersaoDoTermo?> ObterVigenteAsync(CancellationToken cancellationToken = default);

    Task<VersaoDoTermo?> ObterVersaoAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>As versões em ordem de publicação, da primeira à mais recente, paginadas por cursor.</summary>
    Task<(IReadOnlyList<VersaoDoTermo> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarVersoesAsync(
        Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken = default);

    Task AdicionarVersaoAsync(VersaoDoTermo versao, CancellationToken cancellationToken = default);

    Task AdicionarExcecaoDaInstalacaoAsync(TermoNaInstalacao excecao, CancellationToken cancellationToken = default);

    Task<TermoNaInstalacao?> ObterExcecaoDaInstalacaoAsync(Guid instalacaoId, CancellationToken cancellationToken = default);
}
