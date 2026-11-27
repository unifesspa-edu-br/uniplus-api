namespace Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Pagination;

public interface IBaseLegalDoFadaRepository
{
    Task<BaseLegalDoFada?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    Task<BaseLegalDoFada?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>As bases legais vivas com os municípios, para leitura, sem paginação: o cadastro é curto.</summary>
    Task<IReadOnlyList<BaseLegalDoFada>> ListarVivasParaLeituraAsync(CancellationToken cancellationToken);

    Task<(IReadOnlyList<BaseLegalDoFada> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarPaginadoAsync(
        string? afterSortKey,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken);

    /// <summary>Indica se alguma base legal viva já tem o código.</summary>
    Task<bool> CodigoExisteAsync(CodigoDaBaseLegalDoFada codigo, CancellationToken cancellationToken);

    Task AdicionarAsync(BaseLegalDoFada entity, CancellationToken cancellationToken);

    void Remover(BaseLegalDoFada entity);
}
