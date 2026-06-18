namespace Unifesspa.UniPlus.Geo.Application.Abstractions;

using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Leitor read-side de logradouros vinculados a uma Cidade vigente, com busca textual
/// acento/caixa-insensível sobre o nome normalizado.
/// </summary>
public interface ILogradouroReader
{
    Task<(bool CidadeExiste, IReadOnlyList<LogradouroComBairro> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPorCidadeAsync(
        string codigoIbge,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        string? busca,
        CancellationToken cancellationToken);
}

/// <summary>
/// Linha de autocomplete de logradouro enriquecida com o nome do bairro vigente,
/// quando o logradouro aponta para um bairro.
/// </summary>
public sealed record LogradouroComBairro(Logradouro Logradouro, string? BairroNome);
