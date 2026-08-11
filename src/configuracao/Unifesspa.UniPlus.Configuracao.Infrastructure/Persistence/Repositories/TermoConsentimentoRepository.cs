namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em ConfiguracaoInfrastructureRegistration.")]
public sealed class TermoConsentimentoRepository : ITermoConsentimentoRepository
{
    private readonly ConfiguracaoDbContext _dbContext;

    public TermoConsentimentoRepository(ConfiguracaoDbContext dbContext)
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Globalization",
        "CA1304:Specify CultureInfo",
        Justification = "t.Nome.ToLower() vive dentro de uma expression tree traduzida pelo EF Core " +
            "para SQL lower(nome) — roda no Postgres, sem CurrentCulture do processo .NET envolvida.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Globalization",
        "CA1311:Specify a culture or use an invariant version",
        Justification = "Mesma razão de CA1304 — chamada traduzida para SQL, não executada em CLR.")]
    public async Task<(IReadOnlyList<TermoConsentimento> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        string? searchTerm,
        CancellationToken cancellationToken)
    {
        // Sem Include de Versoes — a listagem projeta só o cabeçalho do termo.
        IQueryable<TermoConsentimento> query = _dbContext.TermosConsentimento.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            // Busca indexada via pg_trgm (issue #1105): ILIKE '%termo%' acelerado
            // pelo índice GIN de expressão ix_termo_consentimento_nome_trgm sobre
            // lower(nome) (migration AdicionaBuscaTrigramTermoConsentimento) — o
            // opclass gin_trgm_ops também acelera LIKE/ILIKE, não só os operadores
            // de similaridade. Preferido a word_similarity/<%: abaixo do threshold
            // padrão (0.6) esse operador não garante casar substrings internas
            // curtas (ex.: "lat" não bate com "Plataforma", mesmo com termo válido
            // e presente) — ILIKE dá semântica de "contains" exata, sem depender de
            // limiar algum, para qualquer tamanho de termo. lower() nos dois lados
            // mantém a busca caixa-insensível e casa com o índice; sem
            // acento-insensibilidade (fora de escopo da issue). Curingas do LIKE
            // (% _ \) são escapados para virarem texto literal.
            string term = EscapeLikeWildcards(searchTerm.Trim());
            string pattern = "%" + term + "%";
            const string escape = @"\";
            query = query.Where(t => EF.Functions.ILike(t.Nome.ToLower(), pattern, escape));
        }

        // Keyset bidirecional (ADR-0089): ordenação por Id (Guid v7, ADR-0026/0032),
        // aplicado sobre a query JÁ FILTRADA — os EXISTS internos do helper herdam
        // o mesmo filtro de busca.
        CursorKeysetPage<TermoConsentimento> page = await CursorKeyset
            .ApplyAsync(query, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
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
