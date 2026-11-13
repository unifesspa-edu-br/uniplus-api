namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Domain.Entities;
using Domain.Interfaces;

using Microsoft.EntityFrameworkCore;

public sealed class AtoDoProcessoRepository : IAtoDoProcessoRepository
{
    private readonly SelecaoDbContext _context;

    public AtoDoProcessoRepository(SelecaoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task AdicionarAsync(AtoDoProcesso ato, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ato);
        await _context.AtosDoProcesso.AddAsync(ato, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AtoDoProcesso?> ObterParaMutacaoAsync(Guid atoId, CancellationToken cancellationToken = default)
    {
        return await _context.AtosDoProcesso
            .FirstOrDefaultAsync(a => a.Id == atoId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AtoDoProcesso?> ObterComAnexosAsync(Guid atoId, CancellationToken cancellationToken = default)
    {
        return await _context.AtosDoProcesso
            .AsNoTracking()
            .Include(a => a.Anexos)
            .FirstOrDefaultAsync(a => a.Id == atoId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AtoDoProcesso>> ListarDoProcessoAsync(
        Guid processoSeletivoId,
        CancellationToken cancellationToken = default)
    {
        return await _context.AtosDoProcesso
            .AsNoTracking()
            .Include(a => a.Anexos)
            .Where(a => a.ProcessoSeletivoId == processoSeletivoId)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .AsSplitQuery()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Guid?> ObterProcessoDoAtoAsync(Guid atoId, CancellationToken cancellationToken = default)
    {
        return await _context.AtosDoProcesso
            .AsNoTracking()
            .Where(a => a.Id == atoId)
            .Select(a => (Guid?)a.ProcessoSeletivoId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> ListarCadeiaDeCorrecaoAsync(Guid atoId, CancellationToken cancellationToken = default)
    {
        List<Guid> cadeia = [];
        Guid? atual = atoId;
        while (atual is { } id
            && !cadeia.Contains(id)
            && await ObterEloDaCorrecaoAsync(id, cancellationToken).ConfigureAwait(false) is { } elo)
        {
            cadeia.Add(elo.Id);
            atual = elo.AtoCorrigidoId;
        }

        return cadeia;
    }

    public async Task<Guid?> ObterAtoQueCorrigeAsync(Guid atoCorrigidoId, CancellationToken cancellationToken = default)
    {
        return await _context.AtosDoProcesso
            .AsNoTracking()
            .Where(a => a.AtoCorrigidoId == atoCorrigidoId)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExisteItemDoEditalEntreAsync(IReadOnlyCollection<Guid> atoIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(atoIds);

        Guid[] ids = [.. atoIds];

        return await _context.AtosDoProcesso
            .AsNoTracking()
            .AnyAsync(a => ids.Contains(a.Id) && a.ItemDoEdital != null, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExisteAtoDoTipoForaDaCadeiaAsync(
        Guid processoSeletivoId,
        string tipoCodigo,
        IReadOnlyCollection<Guid> cadeiaDeCorrecao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cadeiaDeCorrecao);

        Guid[] cadeia = [.. cadeiaDeCorrecao];

        return await _context.AtosDoProcesso
            .AsNoTracking()
            .AnyAsync(
                a => a.ProcessoSeletivoId == processoSeletivoId && a.TipoCodigo == tipoCodigo && !cadeia.Contains(a.Id),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> ListarTiposDosAtosAsync(
        IReadOnlyCollection<Guid> atoIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(atoIds);

        Guid[] ids = [.. atoIds];

        return await _context.AtosDoProcesso
            .AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .Select(a => a.TipoCodigo)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlySet<Guid>> ListarDocumentosJaUsadosAsync(
        Guid processoSeletivoId,
        IReadOnlyCollection<Guid> documentoIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentoIds);

        Guid[] ids = [.. documentoIds];

        List<Guid> principais = await DocumentosPrincipaisEntre(processoSeletivoId, ids)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        List<Guid> anexos = await AnexosEntre(processoSeletivoId, ids)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return principais.Concat(anexos).ToHashSet();
    }

    private Task<EloDaCorrecao?> ObterEloDaCorrecaoAsync(Guid atoId, CancellationToken cancellationToken) =>
        _context.AtosDoProcesso
            .AsNoTracking()
            .Where(a => a.Id == atoId)
            .Select(a => new EloDaCorrecao(a.Id, a.AtoCorrigidoId))
            .FirstOrDefaultAsync(cancellationToken);

    private IQueryable<Guid> DocumentosPrincipaisEntre(Guid processoSeletivoId, Guid[] documentoIds) =>
        _context.AtosDoProcesso
            .AsNoTracking()
            .Where(a => a.ProcessoSeletivoId == processoSeletivoId && documentoIds.Contains(a.DocumentoPrincipalId))
            .Select(a => a.DocumentoPrincipalId);

    private IQueryable<Guid> AnexosEntre(Guid processoSeletivoId, Guid[] documentoIds) =>
        _context.AtosDoProcesso
            .AsNoTracking()
            .Where(a => a.ProcessoSeletivoId == processoSeletivoId)
            .SelectMany(a => a.Anexos)
            .Where(x => documentoIds.Contains(x.DocumentoAPublicarId))
            .Select(x => x.DocumentoAPublicarId);

    private sealed record EloDaCorrecao(Guid Id, Guid? AtoCorrigidoId);
}
