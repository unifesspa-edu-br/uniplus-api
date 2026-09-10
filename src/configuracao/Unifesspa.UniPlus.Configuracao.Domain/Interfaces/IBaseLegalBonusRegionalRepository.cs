namespace Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Pagination;

public interface IBaseLegalBonusRegionalRepository
{
    Task<BaseLegalBonusRegional?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    Task<BaseLegalBonusRegional?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken);

    Task<(IReadOnlyList<BaseLegalBonusRegional> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken);

    Task AdicionarAsync(BaseLegalBonusRegional entity, CancellationToken cancellationToken);

    void Remover(BaseLegalBonusRegional entity);
}
