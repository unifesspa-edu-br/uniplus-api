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
        while (atual is { } id && !cadeia.Contains(id))
        {
            var registro = await _context.AtosDoProcesso
                .AsNoTracking()
                .Where(a => a.Id == id)
                .Select(a => new { a.Id, a.AtoCorrigidoId })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (registro is null)
            {
                break;
            }

            cadeia.Add(registro.Id);
            atual = registro.AtoCorrigidoId;
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

        List<Guid> principais = await _context.AtosDoProcesso
            .AsNoTracking()
            .Where(a => a.ProcessoSeletivoId == processoSeletivoId && ids.Contains(a.DocumentoPrincipalId))
            .Select(a => a.DocumentoPrincipalId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<Guid> anexos = await _context.AtosDoProcesso
            .AsNoTracking()
            .Where(a => a.ProcessoSeletivoId == processoSeletivoId)
            .SelectMany(a => a.Anexos)
            .Where(x => ids.Contains(x.DocumentoAPublicarId))
            .Select(x => x.DocumentoAPublicarId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return principais.Concat(anexos).ToHashSet();
    }
}
