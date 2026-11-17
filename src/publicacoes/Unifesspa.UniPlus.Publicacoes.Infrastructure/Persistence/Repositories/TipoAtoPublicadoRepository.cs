namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.Repositories;

using System.Linq;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

public sealed class TipoAtoPublicadoRepository : ITipoAtoPublicadoRepository
{
    private readonly UniPlusDbContext _dbContext;

    public TipoAtoPublicadoRepository(UniPlusDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<TipoAtoPublicado?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.TiposAtoPublicado
            .FirstOrDefaultAsync(tipo => tipo.Id == id, cancellationToken);
    }

    public Task<TipoAtoPublicado?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.TiposAtoPublicado
            .AsNoTracking()
            .FirstOrDefaultAsync(tipo => tipo.Id == id, cancellationToken);
    }

    /// <remarks>
    /// <c>SingleOrDefault</c>, e não <c>FirstOrDefault</c>: sob a exclusion constraint existe no
    /// máximo uma versão viva vigente na data. Duas seria corrupção do catálogo, e devolver "a
    /// primeira" entregaria em silêncio um congela_configuracao possivelmente errado a quem for
    /// validar o ato.
    /// </remarks>
    public Task<TipoAtoPublicado?> ObterVigenteAsync(
        string codigo,
        DateOnly data,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);

        string codigoNormalizado = codigo.Trim();

        return _dbContext.TiposAtoPublicado
            .AsNoTracking()
            .SingleOrDefaultAsync(
                tipo => tipo.Codigo == codigoNormalizado
                    && tipo.VigenciaInicio <= data
                    && (tipo.VigenciaFim == null || tipo.VigenciaFim > data),
                cancellationToken);
    }

    /// <remarks>
    /// Sem <c>OrderBy</c> aqui: o helper keyset (ADR-0089) ordena e fatia. Só os filtros se
    /// aplicam, e os <c>EXISTS</c> de flag do helper herdam a mesma consulta base.
    /// </remarks>
    public async Task<(IReadOnlyList<TipoAtoPublicado> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        DateOnly? vigentesEm,
        CancellationToken cancellationToken)
    {
        IQueryable<TipoAtoPublicado> consulta = _dbContext.TiposAtoPublicado.AsNoTracking();

        if (vigentesEm is { } dia)
        {
            consulta = consulta.Where(TipoAtoPublicado.VigenteEm(dia));
        }

        CursorKeysetPage<TipoAtoPublicado> pagina = await CursorKeyset
            .ApplyAsync(consulta, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (pagina.Items, pagina.PrevAfterId, pagina.NextAfterId);
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

    /// <remarks>
    /// Duas janelas semiabertas [a1,a2) e [b1,b2) se interceptam quando a1 &lt; b2 e b1 &lt; a2.
    /// Fim nulo é +infinito, e o lado correspondente da conjunção some.
    /// </remarks>
    public Task<bool> ExisteSobreposicaoDeVigenciaAsync(
        string codigo,
        DateOnly vigenciaInicio,
        DateOnly? vigenciaFim,
        Guid? excluirId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);

        string codigoNormalizado = codigo.Trim();

        return _dbContext.TiposAtoPublicado
            .AsNoTracking()
            .Where(tipo => excluirId == null || tipo.Id != excluirId)
            .AnyAsync(
                tipo => tipo.Codigo == codigoNormalizado
                    && (vigenciaFim == null || tipo.VigenciaInicio < vigenciaFim)
                    && (tipo.VigenciaFim == null || tipo.VigenciaFim > vigenciaInicio),
                cancellationToken);
    }
}
