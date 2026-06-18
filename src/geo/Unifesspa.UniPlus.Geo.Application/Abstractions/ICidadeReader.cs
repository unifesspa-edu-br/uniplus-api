namespace Unifesspa.UniPlus.Geo.Application.Abstractions;

using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Leitor read-side de <see cref="Cidade"/> para a API pública de reference data
/// (ADR-0090). Expõe só o que vigente (ADR-0092). A abstração trafega primitivas +
/// <see cref="FiltroListagemCidades"/> + entidades de domínio (nunca
/// <c>IQueryable</c>/<c>DbContext</c>), mantendo Application independente de EF Core.
/// </summary>
public interface ICidadeReader
{
    /// <summary>
    /// Lista as Cidades vigentes paginadas por keyset bidirecional sobre <c>Id</c>
    /// (ADR-0026 + ADR-0089), aplicando o <paramref name="filtro"/> (UF + busca
    /// textual). Os <c>EXISTS</c> do keyset herdam o mesmo <c>WHERE</c> filtrado.
    /// </summary>
    Task<(IReadOnlyList<Cidade> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        FiltroListagemCidades filtro,
        CancellationToken cancellationToken);

    /// <summary>
    /// Obtém a Cidade vigente pela chave natural <paramref name="codigoIbge"/>,
    /// acompanhada do indicador socioeconômico 1:1 (quando existir), num único
    /// snapshot consistente; <see langword="null"/> quando a cidade inexiste.
    /// </summary>
    Task<CidadeComIndicador?> ObterDetalhePorCodigoIbgeAsync(string codigoIbge, CancellationToken cancellationToken);
}

/// <summary>
/// Cidade + seu indicador socioeconômico 1:1 (<see langword="null"/> quando o
/// município não tem satélite). Retorno read-only do <see cref="ICidadeReader"/>;
/// o handler projeta em <c>CidadeDetalheDto</c>.
/// </summary>
public sealed record CidadeComIndicador(Cidade Cidade, CidadeIndicador? Indicador);
