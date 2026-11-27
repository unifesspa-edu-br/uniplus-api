namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed class TermoConsentimentoRepository : ITermoConsentimentoRepository
{
    private readonly UniPlusDbContext _dbContext;

    public TermoConsentimentoRepository(UniPlusDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<TermoConsentimento?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.TermosConsentimento
            .Include(t => t.Versoes)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public Task<TermoConsentimento?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.TermosConsentimento
            .AsNoTracking()
            .Include(t => t.Versoes)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<TermoConsentimento> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarPaginadoAsync(
        string? afterSortKey,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        string? searchTerm,
        CancellationToken cancellationToken)
    {
        string? termo = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim();
        OrderedKeysetPage<TermoConsentimento> page = await CursorKeyset
            .ApplyAsync(
                FiltrarPorNome(_dbContext.TermosConsentimento.AsNoTracking(), termo),
                CondicoesDoFiltro.De(ParteDoFiltro.De(termo)),
                afterSortKey,
                afterId,
                limit,
                direction,
                cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.Previous, page.Next);
    }

    // Busca contida e insensível a caixa por ILIKE, acelerada pelo índice GIN ix_termos_de_consentimento_nome_trgm sobre
    // nome (script 003_busca_por_proximidade_do_termo.sql). Sensível a acento. Os curingas do LIKE são escapados.
    private static IQueryable<TermoConsentimento> FiltrarPorNome(IQueryable<TermoConsentimento> query, string? termo)
    {
        if (termo is null)
        {
            return query;
        }

        string pattern = "%" + EscapeLikeWildcards(termo) + "%";
        const string escape = @"\";
        return query.Where(t => EF.Functions.ILike(t.Nome, pattern, escape));
    }

    // Escapa os curingas do LIKE/ILIKE (\ % _) para que metacaracteres digitados
    // pelo usuário sejam comparados como texto literal, não wildcards — sem isso,
    // searchTerm="_" ou searchTerm="%" casaria quase qualquer registro. Barra
    // invertida escapada primeiro para não duplicar as inseridas ao escapar % e _.
    private static string EscapeLikeWildcards(string term) =>
        term
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    public Task<bool> CodigoExisteAsync(CodigoTermoConsentimento codigo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        return _dbContext.TermosConsentimento.AsNoTracking().AnyAsync(t => t.Codigo == codigo, cancellationToken);
    }

    public async Task AdicionarAsync(TermoConsentimento termo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(termo);
        await _dbContext.TermosConsentimento.AddAsync(termo, cancellationToken).ConfigureAwait(false);
    }

    public async Task AdicionarVersaoAsync(TermoConsentimento termo, TermoConsentimentoVersao versao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(termo);
        ArgumentNullException.ThrowIfNull(versao);

        // Nenhum campo do próprio TermoConsentimento muda ao promover — sem marcar
        // uma propriedade real como modificada, o EF Core não emite UPDATE nenhum
        // para essa linha, e o token de concorrência otimista (xmin) nunca é
        // conferido. RevisadoEm é reescrito com o MESMO valor só para forçar o
        // UPDATE amarrado ao xmin lido na consulta.
        _dbContext.Entry(termo).Property(nameof(TermoConsentimento.RevisadoEm)).IsModified = true;

        await _dbContext.VersoesTermoConsentimento.AddAsync(versao, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(TermoConsentimento termo)
    {
        ArgumentNullException.ThrowIfNull(termo);
        _dbContext.TermosConsentimento.Remove(termo);
    }
}
