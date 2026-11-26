namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Reader cross-módulo dos tipos ativos; não expõe itens desativados.</summary>
internal sealed class TipoEtapaReader : ITipoEtapaReader
{
    private readonly UniPlusDbContext _dbContext;

    public TipoEtapaReader(UniPlusDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TipoEtapaView>> ListarAtivosAsync(CancellationToken cancellationToken = default)
    {
        List<TipoEtapa> tipos = await _dbContext.TiposEtapa.AsNoTracking().Include(tipo => tipo.ConceitosAdmitidos)
            .Where(tipo => tipo.Ativo).OrderBy(tipo => tipo.Codigo)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return [.. tipos.Select(ParaView)];
    }

    public async Task<TipoEtapaView?> ObterAtivoPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        TipoEtapa? tipo = await _dbContext.TiposEtapa.AsNoTracking().Include(tipo => tipo.ConceitosAdmitidos)
            .FirstOrDefaultAsync(tipo => tipo.Id == id && tipo.Ativo, cancellationToken).ConfigureAwait(false);
        return tipo is null ? null : ParaView(tipo);
    }

    public async Task<TipoEtapaView?> ObterAtivoPorCodigoAsync(string codigo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        TipoEtapa? tipo = await _dbContext.TiposEtapa.AsNoTracking().Include(tipo => tipo.ConceitosAdmitidos)
            .FirstOrDefaultAsync(tipo => tipo.Codigo == codigo && tipo.Ativo, cancellationToken).ConfigureAwait(false);
        return tipo is null ? null : ParaView(tipo);
    }

    public async Task<IReadOnlyList<TipoEtapaView>> ListarAtivosPorCodigosAsync(
        IReadOnlyCollection<string> codigos, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(codigos);

        List<string> codigosDoLote = [.. codigos.Distinct(StringComparer.Ordinal)];
        List<TipoEtapa> tipos = await _dbContext.TiposEtapa.AsNoTracking().Include(tipo => tipo.ConceitosAdmitidos)
            .Where(tipo => tipo.Ativo && codigosDoLote.Contains(tipo.Codigo)).OrderBy(tipo => tipo.Codigo)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return [.. tipos.Select(ParaView)];
    }

    private static TipoEtapaView ParaView(TipoEtapa tipo) =>
        new(tipo.Id, tipo.Codigo, tipo.Nome, tipo.Descricao,
            new AvaliacaoDasEtapasView(
                tipo.AdmitePontuacao, tipo.AdmiteEliminacao, tipo.NotaDeOrigemNoEnem, tipo.ExigeCorrecaoIndependente,
                [.. tipo.ConceitosAdmitidos.Select(conceito => new ConceitoAdmitidoView(
                    conceito.Codigo, conceito.Nome, conceito.Natureza.Codigo()))]));
}
