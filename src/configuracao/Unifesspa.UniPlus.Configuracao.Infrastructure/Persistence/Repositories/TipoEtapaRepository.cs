namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed class TipoEtapaRepository : ITipoEtapaRepository
{
    private readonly UniPlusDbContext _dbContext;

    public TipoEtapaRepository(UniPlusDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<TipoEtapa?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.TiposEtapa.Include(tipo => tipo.ConceitosAdmitidos).FirstOrDefaultAsync(tipo => tipo.Id == id, cancellationToken);

    public Task<TipoEtapa?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.TiposEtapa.AsNoTracking().Include(tipo => tipo.ConceitosAdmitidos)
            .FirstOrDefaultAsync(tipo => tipo.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<TipoEtapa> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken)
    {
        CursorKeysetPage<TipoEtapa> page = await CursorKeyset
            .ApplyAsync(
                _dbContext.TiposEtapa.AsNoTracking().Include(tipo => tipo.ConceitosAdmitidos).Where(tipo => tipo.Ativo), afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);
        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }

    public async Task AdicionarAsync(TipoEtapa tipo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        await _dbContext.TiposEtapa.AddAsync(tipo, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> CodigoExisteAsync(string codigo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        // O cadastro guarda o código em NFC e a igualdade do banco é ordinal: a consulta normaliza antes,
        // porque o EF não traduziria a normalização para SQL. Texto que nem normaliza não é código cadastrado.
        return TextoNormalizavel.TentarNormalizar(codigo.Trim(), out string codigoNormalizado)
            ? _dbContext.TiposEtapa.AsNoTracking().AnyAsync(tipo => tipo.Codigo == codigoNormalizado, cancellationToken)
            : Task.FromResult(false);
    }
}
