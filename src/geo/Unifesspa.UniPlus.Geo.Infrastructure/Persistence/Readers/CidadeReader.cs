namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Readers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;

using MR.EntityFrameworkCore.KeysetPagination;

using Unifesspa.UniPlus.Geo.Application.Abstractions;
using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Leitor read-side de <see cref="Cidade"/> sobre o <see cref="GeoDbContext"/>.
/// Só expõe reference data vigente (ADR-0092). Listagem por keyset bidirecional
/// ordenado alfabeticamente por nome (<see cref="KeysetOrdenadoCursor"/>, ADR-0094)
/// com filtro por UF e busca textual acento/caixa-insensível sobre
/// <c>nome_normalizado</c> (índice trigram <c>gin_trgm_ops</c>).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em GeoInfrastructureRegistration.")]
public sealed class CidadeReader : ICidadeReader
{
    private readonly GeoDbContext _dbContext;

    public CidadeReader(GeoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<(IReadOnlyList<Cidade> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarPaginadoAsync(
        string? afterSortKey,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        FiltroListagemCidades filtro,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        IQueryable<Cidade> query = _dbContext.Cidades
            .AsNoTracking()
            .Where(c => c.Vigente);

        if (filtro.TemUf)
        {
            // UF normalizada para maiúsculas (espelha Cidade.Importar); inexistente
            // produz lista vazia, não erro — UF é filtro, não recurso.
            string ufNormalizada = filtro.Uf!.Trim().ToUpperInvariant();
            query = query.Where(c => c.Uf == ufNormalizada);
        }

        if (filtro.TemBusca)
        {
            // Busca acento/caixa-insensível: nome_normalizado já vem sem acento da
            // fonte (cidade_sem_acento) — NÃO há immutable_unaccent em runtime. O termo
            // é normalizado no app (NFD + remoção de NonSpacingMark) e comparado via
            // ILIKE (cuida da caixa). Os curingas %, _ e \ são escapados para serem
            // tratados como texto literal; o escape char é passado explicitamente
            // (a sobrecarga de 2 args do ILike emite ESCAPE '', desligando o \).
            // Guard NomeNormalizado != null: ILIKE NULL não casa — cidades sem
            // nome_normalizado simplesmente não aparecem na busca (aparecem sem filtro).
            string pattern = BuscaTextualNormalizada.CriarPadraoContem(filtro.Termo!);
            query = query.Where(c =>
                c.NomeNormalizado != null &&
                EF.Functions.ILike(c.NomeNormalizado, pattern, BuscaTextualNormalizada.Escape));
        }

        // Keyset ordenado por nome_ordenacao (ADR-0094/0095) sobre a query JÁ filtrada — ordenação,
        // âncora (sort key + Id), reversão e flags ficam no helper; os EXISTS herdam o
        // WHERE. A chave de ordenação é gerada no banco com fallback normalizado de Nome.
        KeysetOrdenadoPage<Cidade> page = await KeysetOrdenadoCursor
            .ApplyAsync(
                query,
                b => b.Ascending(c => c.NomeOrdenacao).Ascending(c => c.Id),
                c => c.NomeOrdenacao,
                static (sortKey, id) => new { NomeOrdenacao = sortKey, Id = id },
                afterSortKey,
                afterId,
                limit,
                direction,
                cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.Anterior, page.Proximo);
    }

    public async Task<CidadeComIndicador?> ObterDetalhePorCodigoIbgeAsync(
        string codigoIbge,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigoIbge);

        string codigo = codigoIbge.Trim();

        // Projeção única (um round-trip, snapshot consistente sob READ COMMITTED):
        // cidade vigente + indicador 1:1 vigente via subconsulta correlacionada —
        // Cidade e CidadeIndicador não têm navigation property (ver
        // CidadeIndicadorConfiguration), então o vínculo é resolvido por CidadeId.
        return await _dbContext.Cidades
            .AsNoTracking()
            .Where(c => c.Vigente && c.CodigoIbge == codigo)
            .Select(c => new CidadeComIndicador(
                c,
                _dbContext.CidadeIndicadores
                    .Where(i => i.Vigente && i.CidadeId == c.Id)
                    .FirstOrDefault()))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

}
