namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Lê as notas vigentes sem expor de onde vieram. A vigência é a do lote mais recente do escopo, e
/// o lançamento só vale enquanto a inscrição ainda escolhe a edição do lote: quem trocou de edição
/// depois da importação fica sem nota até a próxima importação.
/// </summary>
public sealed class LancamentosDeNotaReader : ILancamentosDeNotaReader
{
    private readonly SelecaoDbContext _context;

    public LancamentosDeNotaReader(SelecaoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<IReadOnlyList<LancamentoVigente>> ListarVigentesAsync(
        Guid etapaProcessoId, IReadOnlyCollection<Guid> inscricaoIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inscricaoIds);

        Guid[] ids = [.. inscricaoIds];

        return await (
            from lancamento in _context.LancamentosDeNota.AsNoTracking()
            join lote in VigenciaDasNotasDoEnem.LotesVigentes(_context) on lancamento.LoteId equals lote.Id
            where lancamento.EtapaProcessoId == etapaProcessoId
                && ids.Contains(lancamento.InscricaoId)
                && VigenciaDasNotasDoEnem.EdicoesEscolhidas(_context).Any(r => r.InscricaoId == lancamento.InscricaoId
                    && r.ValorCodigo == lote.EdicaoCodigo)
            select new LancamentoVigente(
                lancamento.InscricaoId,
                lancamento.EtapaProcessoId,
                lancamento.ItemCodigo,
                lancamento.Nota,
                lancamento.Situacao,
                lancamento.LoteId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
