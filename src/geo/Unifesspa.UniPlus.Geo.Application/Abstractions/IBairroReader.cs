namespace Unifesspa.UniPlus.Geo.Application.Abstractions;

using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Leitor read-side de bairros vinculados a uma Cidade vigente, com busca textual
/// acento/caixa-insensível sobre o nome normalizado.
/// </summary>
public interface IBairroReader
{
    Task<(bool CidadeExiste, IReadOnlyList<Bairro> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPorCidadeAsync(
        string codigoIbge,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        string? busca,
        CancellationToken cancellationToken);
}
