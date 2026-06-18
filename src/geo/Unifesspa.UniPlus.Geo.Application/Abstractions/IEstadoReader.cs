namespace Unifesspa.UniPlus.Geo.Application.Abstractions;

using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Leitor read-side de <see cref="Estado"/> (UF) para a API pública de reference
/// data (ADR-0090). Expõe só o que vigente (<c>vigente=true</c>) — linhas stale do
/// ETL (ADR-0092) não vazam. A abstração trafega primitivas + entidades de
/// domínio (nunca <c>IQueryable</c>/<c>DbContext</c>), mantendo Application
/// independente de EF Core.
/// </summary>
public interface IEstadoReader
{
    /// <summary>
    /// Lista os Estados vigentes paginados por keyset bidirecional sobre <c>Id</c>
    /// (ADR-0026 + ADR-0089). Retorna as âncoras para o controller emitir os
    /// cursores <c>prev</c>/<c>next</c>.
    /// </summary>
    Task<(IReadOnlyList<Estado> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken);

    /// <summary>
    /// Obtém um Estado vigente pela chave natural <paramref name="uf"/>
    /// (normalizada para maiúsculas); <see langword="null"/> quando inexistente.
    /// </summary>
    Task<Estado?> ObterPorUfAsync(string uf, CancellationToken cancellationToken);
}
