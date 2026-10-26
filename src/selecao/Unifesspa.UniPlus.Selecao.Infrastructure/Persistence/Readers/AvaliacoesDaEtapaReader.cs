namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Readers;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Lê as avaliações vigentes. Vale o lote mais recente do escopo, e a avaliação só vale enquanto a
/// inscrição ainda escolhe a edição do lote e ainda concorre à oferta avaliada: uma correção da
/// inscrição depois da importação esconde a avaliação que deixou de bater, até o próximo lote.
/// </summary>
public sealed class AvaliacoesDaEtapaReader : IAvaliacoesDaEtapaReader
{
    private readonly SelecaoDbContext _context;

    public AvaliacoesDaEtapaReader(SelecaoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<IReadOnlyList<AvaliacaoVigente>> ListarVigentesAsync(
        Guid etapaProcessoId, IReadOnlyCollection<Guid> inscricaoIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inscricaoIds);

        Guid[] ids = [.. inscricaoIds];

        return await (
            from avaliacao in _context.AvaliacoesDaEtapa.AsNoTracking()
            join lote in VigenciaDasNotasDoEnem.LotesVigentes(_context) on avaliacao.LoteId equals lote.Id
            where avaliacao.EtapaProcessoId == etapaProcessoId
                && ids.Contains(avaliacao.InscricaoId)
                && VigenciaDasNotasDoEnem.EdicoesEscolhidas(_context).Any(r => r.InscricaoId == avaliacao.InscricaoId
                    && r.ValorCodigo == lote.EdicaoCodigo)
                && _context.Inscricoes.Any(i => i.Id == avaliacao.InscricaoId
                    && (i.OfertaCursoId == avaliacao.OfertaCursoId || i.OfertaCursoSegundaOpcaoId == avaliacao.OfertaCursoId))
            select new AvaliacaoVigente(
                avaliacao.InscricaoId,
                avaliacao.EtapaProcessoId,
                avaliacao.OfertaCursoId,
                avaliacao.GrupoAreaEnemCodigo,
                avaliacao.Situacao,
                avaliacao.Nota,
                avaliacao.LoteId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
