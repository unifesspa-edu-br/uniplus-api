namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em PublicacoesInfrastructureRegistration.")]
public sealed class TipoAtoPublicadoRepository : ITipoAtoPublicadoRepository
{
    private readonly PublicacoesDbContext _dbContext;

    public TipoAtoPublicadoRepository(PublicacoesDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<TipoAtoPublicado?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.TiposAtoPublicado
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public Task<TipoAtoPublicado?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.TiposAtoPublicado
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public Task<TipoAtoPublicado?> ObterVigenteAsync(
        string codigo,
        DateOnly data,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);

        string codigoNorm = codigo.Trim();

        // SingleOrDefault, não FirstOrDefault: sob a exclusion constraint existe no
        // máximo uma versão viva vigente na data. Duas seria corrupção do catálogo,
        // e devolver "a primeira" entregaria em silêncio um congela_configuracao
        // possivelmente errado a quem for validar o ato.
        return _dbContext.TiposAtoPublicado
            .AsNoTracking()
            .SingleOrDefaultAsync(
                t => t.Codigo == codigoNorm
                    && t.VigenciaInicio <= data
                    && (t.VigenciaFim == null || t.VigenciaFim > data),
                cancellationToken);
    }

    public async Task<(IReadOnlyList<TipoAtoPublicado> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        // Keyset bidirecional (ADR-0089): ordenação por Id (Guid v7, ADR-0026/0032).
        CursorKeysetPage<TipoAtoPublicado> page = await CursorKeyset
            .ApplyAsync(_dbContext.TiposAtoPublicado.AsNoTracking(), afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }

    public async Task AdicionarAsync(TipoAtoPublicado tipo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        await _dbContext.TiposAtoPublicado.AddAsync(tipo, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(TipoAtoPublicado tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        _dbContext.TiposAtoPublicado.Remove(tipo);
    }

    public Task<bool> ExisteSobreposicaoDeVigenciaAsync(
        string codigo,
        DateOnly vigenciaInicio,
        DateOnly? vigenciaFim,
        Guid? excluirId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);

        string codigoNorm = codigo.Trim();

        // Duas janelas semiabertas [a1,a2) e [b1,b2) se interceptam quando
        // a1 < b2 e b1 < a2. Fim nulo é +infinito, e o lado correspondente da
        // conjunção some.
        return _dbContext.TiposAtoPublicado
            .AsNoTracking()
            .Where(t => excluirId == null || t.Id != excluirId)
            .AnyAsync(
                t => t.Codigo == codigoNorm
                    && (vigenciaFim == null || t.VigenciaInicio < vigenciaFim)
                    && (t.VigenciaFim == null || t.VigenciaFim > vigenciaInicio),
                cancellationToken);
    }
}
