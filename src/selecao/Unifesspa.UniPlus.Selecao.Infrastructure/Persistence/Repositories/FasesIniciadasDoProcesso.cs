namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Quais fases da versão publicada vigente já começaram: pela janela que começou ou terminou, pelo ato do tipo que a
/// fase declara, ou pela inscrição recebida na fase que a coleta. A leitura é da versão publicada, e não da edição
/// em curso, de modo que mover a janela no rascunho não faz a fase deixar de ter começado.
/// </summary>
internal sealed class FasesIniciadasDoProcesso : IFasesIniciadasDoProcesso
{
    private readonly SelecaoDbContext _context;

    public FasesIniciadasDoProcesso(SelecaoDbContext context) => _context = context;

    public async Task<FasesIniciadas> ObterAsync(Guid processoId, DateTimeOffset agora, CancellationToken cancellationToken = default)
    {
        string? envelope = await _context.VersoesConfiguracao
            .AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId)
            .OrderByDescending(v => v.NumeroVersao)
            .Select(v => v.ConfiguracaoCongelada)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<FasesNoEnvelope.Fase> fases = FasesNoEnvelope.Das(envelope);
        HashSet<string> atosRegistrados = await AtosRegistradosAsync(processoId, cancellationToken).ConfigureAwait(false);
        bool haInscricao = await _context.Inscricoes
            .AsNoTracking()
            .AnyAsync(i => i.ProcessoSeletivoId == processoId && i.Situacao != SituacaoInscricao.Rascunho, cancellationToken)
            .ConfigureAwait(false);

        return new FasesIniciadas([.. fases
            .Where(f => Iniciada(f, agora, atosRegistrados, haInscricao))
            .Select(static f => f.OrigemId)]);
    }

    private static bool Iniciada(FasesNoEnvelope.Fase fase, DateTimeOffset agora, HashSet<string> atosRegistrados, bool haInscricao) =>
        fase.Inicio <= agora
        || fase.Fim <= agora
        || fase.AtosDeclarados.Any(atosRegistrados.Contains)
        || (fase.ColetaInscricao && haInscricao);

    private async Task<HashSet<string>> AtosRegistradosAsync(Guid processoId, CancellationToken cancellationToken) =>
        [.. await _context.AtosDoProcesso
            .AsNoTracking()
            .Where(a => a.ProcessoSeletivoId == processoId)
            .Select(a => a.TipoCodigo)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false)];
}
