namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

/// <summary>Reader cross-módulo dos tipos ativos; não expõe itens desativados.</summary>
internal sealed class TipoProcessoReader : ITipoProcessoReader
{
    private readonly ConfiguracaoDbContext _dbContext;

    public TipoProcessoReader(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TipoProcessoView>> ListarAtivosAsync(CancellationToken cancellationToken = default)
    {
        List<TipoProcesso> tipos = await _dbContext.TiposProcesso.AsNoTracking()
            .Where(tipo => tipo.Ativo).OrderBy(tipo => tipo.Codigo)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return [.. tipos.Select(ParaView)];
    }

    public async Task<TipoProcessoView?> ObterAtivoPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        TipoProcesso? tipo = await _dbContext.TiposProcesso.AsNoTracking()
            .FirstOrDefaultAsync(tipo => tipo.Id == id && tipo.Ativo, cancellationToken).ConfigureAwait(false);
        return tipo is null ? null : ParaView(tipo);
    }

    public async Task<TipoProcessoView?> ObterAtivoPorCodigoAsync(string codigo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        TipoProcesso? tipo = await _dbContext.TiposProcesso.AsNoTracking()
            .FirstOrDefaultAsync(tipo => tipo.Codigo == codigo.Trim() && tipo.Ativo, cancellationToken).ConfigureAwait(false);
        return tipo is null ? null : ParaView(tipo);
    }

    private static TipoProcessoView ParaView(TipoProcesso tipo) =>
        new(tipo.Id, tipo.Codigo, tipo.Nome, tipo.Descricao);
}
