namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Readers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using Unifesspa.UniPlus.Geo.Application.Abstractions;
using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Leitor read-side de <see cref="Logradouro"/> sobre o <see cref="GeoDbContext"/>,
/// sempre restrito a uma Cidade vigente e com busca textual opcional.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em GeoInfrastructureRegistration.")]
internal sealed class LogradouroReader : ILogradouroReader
{
    private readonly GeoDbContext _dbContext;

    public LogradouroReader(GeoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<(bool CidadeExiste, IReadOnlyList<LogradouroComBairro> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPorCidadeAsync(
        string codigoIbge,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        string? busca,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoIbge);

        if (await ResolverCidadeIdAsync(codigoIbge, cancellationToken).ConfigureAwait(false) is not { } cidadeId)
        {
            return (false, [], null, null);
        }

        IQueryable<Logradouro> escopoCidade = _dbContext.Logradouros
            .AsNoTracking()
            .Where(l => l.Vigente && l.CidadeId == cidadeId);

        return string.IsNullOrWhiteSpace(busca)
            ? await ListarNavegandoAsync(escopoCidade, afterId, limit, direction, cancellationToken).ConfigureAwait(false)
            : await BuscarPorRelevanciaAsync(escopoCidade, busca, limit, cancellationToken).ConfigureAwait(false);
    }

    // Navegação (sem busca): lista da cidade paginada por cursor keyset bidirecional
    // (ADR-0089), ordenada por Id.
    private async Task<(bool, IReadOnlyList<LogradouroComBairro>, Guid?, Guid?)> ListarNavegandoAsync(
        IQueryable<Logradouro> escopoCidade,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        CursorKeysetPage<Logradouro> page = await CursorKeyset
            .ApplyAsync(escopoCidade, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<LogradouroComBairro> itens = await EnriquecerComBairroAsync(page.Items, cancellationToken)
            .ConfigureAwait(false);

        return (true, itens, page.PrevAfterId, page.NextAfterId);
    }

    // Autocomplete (com busca): filtra por word_similarity (pg_trgm, operador `<%`) e
    // ordena por relevância. Ao contrário do ILIKE (substring contígua, frágil), o
    // word_similarity tolera abreviação de tipo ("av brasil" → "Avenida Brasil"), ordem
    // das palavras ("paulista avenida") e pequenos erros de digitação — sem dicionário de
    // abreviações (#709). O operador `<%` reaproveita o índice gin_trgm_ops
    // (ix_logradouro_nome_trgm) que a #707 criou — sem migration. O ranking
    // `word_similarity DESC, similarity DESC` coloca a match exata no topo: o desempate
    // por `similarity` global separa "Rua das Flores" de "Rua das Flores e Jardins", que
    // empatam em word_similarity = 1. Typo puro tem recall ok mas ranking subótimo —
    // limitação aceita para autocomplete (ver evidência do spike na #709).
    //
    // O ranking por similaridade é incompatível com o keyset por Id (ADR-0089), então
    // retorna o top-N sem âncoras de paginação (sem prev/next): a navegação profunda não
    // faz sentido para autocomplete.
    private async Task<(bool, IReadOnlyList<LogradouroComBairro>, Guid?, Guid?)> BuscarPorRelevanciaAsync(
        IQueryable<Logradouro> escopoCidade,
        string busca,
        int limit,
        CancellationToken cancellationToken)
    {
        string termo = BuscaTextualNormalizada.Normalizar(busca);

        // O filtro `<%` decide o match comparando pg_trgm.word_similarity_threshold, lido
        // em tempo de execução. SET LOCAL o fixa em 0.6 (default do PostgreSQL; calibrado
        // contra o DNE real na #709 — mantém a match esperada em 1º com baixo volume de
        // falsos positivos) de forma transação-local: determinístico, sem depender da
        // config global do servidor e sem vazar para a conexão devolvida ao pool. SET LOCAL
        // exige um bloco de transação. No caminho de query atual o handler não roda sob
        // transação ambiente (verificado nos testes de integração, que sobem o runtime
        // Wolverine real), então abre a sua própria; se uma transação ambiente já existir,
        // reaproveita-a sem assumir seu ciclo de vida — evita abrir uma transação aninhada
        // e blinda contra mudança futura no pipeline.
        IDbContextTransaction? ambiente = _dbContext.Database.CurrentTransaction;
        IDbContextTransaction transacao = ambiente
            ?? await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        List<Logradouro> rankeados;
        try
        {
            // String literal constante (sem interpolação) — não dispara o EF1002.
            await _dbContext.Database
                .ExecuteSqlRawAsync("SET LOCAL pg_trgm.word_similarity_threshold = 0.6", cancellationToken)
                .ConfigureAwait(false);

            rankeados = await escopoCidade
                .Where(l => EF.Functions.TrigramsAreWordSimilar(termo, l.NomeNormalizado))
                .OrderByDescending(l => EF.Functions.TrigramsWordSimilarity(termo, l.NomeNormalizado))
                .ThenByDescending(l => EF.Functions.TrigramsSimilarity(l.NomeNormalizado, termo))
                .ThenBy(l => l.Id)
                .Take(limit)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            // Commita/descarta apenas a transação que este método abriu; uma ambiente é
            // de responsabilidade de quem a iniciou.
            if (ambiente is null)
            {
                await transacao.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (ambiente is null)
            {
                await transacao.DisposeAsync().ConfigureAwait(false);
            }
        }

        IReadOnlyList<LogradouroComBairro> itens = await EnriquecerComBairroAsync(rankeados, cancellationToken)
            .ConfigureAwait(false);

        return (true, itens, null, null);
    }

    private async Task<IReadOnlyList<LogradouroComBairro>> EnriquecerComBairroAsync(
        IReadOnlyList<Logradouro> logradouros,
        CancellationToken cancellationToken)
    {
        if (logradouros.Count == 0)
        {
            return [];
        }

        Guid[] bairroIds = [.. logradouros
            .Select(l => l.BairroId)
            .OfType<Guid>()
            .Distinct()];

        Dictionary<Guid, string> bairros = bairroIds.Length == 0
            ? []
            : await _dbContext.Bairros
                .AsNoTracking()
                .Where(b => b.Vigente && bairroIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id, b => b.Nome, cancellationToken)
                .ConfigureAwait(false);

        return [.. logradouros.Select(l =>
            new LogradouroComBairro(
                l,
                l.BairroId is { } bairroId && bairros.TryGetValue(bairroId, out string? nome) ? nome : null))];
    }

    private Task<Guid?> ResolverCidadeIdAsync(string codigoIbge, CancellationToken cancellationToken)
    {
        string codigo = codigoIbge.Trim();
        return _dbContext.Cidades
            .AsNoTracking()
            .Where(c => c.Vigente && c.CodigoIbge == codigo)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
