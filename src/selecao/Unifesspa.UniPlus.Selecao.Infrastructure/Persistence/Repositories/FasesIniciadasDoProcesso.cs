namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Quais fases da versão publicada vigente já começaram: pela janela que começou ou terminou, pelo ato do item que a
/// fase declara, pela avaliação já lançada numa etapa dela, ou pela inscrição recebida na fase que a coleta. A leitura
/// é da versão publicada, e não da edição em curso, de modo que mover a janela no rascunho não faz a fase deixar de
/// ter começado.
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
        List<string> itensComAto = await ItensComAtoRegistradoAsync(processoId, cancellationToken).ConfigureAwait(false);
        HashSet<Guid> etapasComAvaliacao = await EtapasComAvaliacaoAsync(fases, cancellationToken).ConfigureAwait(false);
        bool haInscricao = await _context.Inscricoes
            .AsNoTracking()
            .AnyAsync(i => i.ProcessoSeletivoId == processoId && i.Situacao != SituacaoInscricao.Rascunho, cancellationToken)
            .ConfigureAwait(false);

        return new FasesIniciadas([.. fases
            .Where(f => Iniciada(f, agora, itensComAto, etapasComAvaliacao, haInscricao))
            .Select(static f => KeyValuePair.Create(f.OrigemId, f.Unidade))]);
    }

    // O ato guarda o lugar do item que o edital declara: a fase de origem, ou a etapa. O tipo do ato sozinho não
    // serve, porque duas fases podem declarar o mesmo tipo e o ato de uma não inicia a outra.
    private static bool Iniciada(
        FasesNoEnvelope.Fase fase, DateTimeOffset agora, List<string> itensComAto, HashSet<Guid> etapasComAvaliacao, bool haInscricao) =>
        fase.Inicio <= agora
        || fase.Fim <= agora
        || itensComAto.Any(item => DoLugar(item, $"fase:{fase.OrigemId:D}") || fase.Etapas.Any(e => DoLugar(item, $"etapa:{e:D}")))
        || fase.Etapas.Any(etapasComAvaliacao.Contains)
        || (fase.ColetaInscricao && haInscricao);

    private async Task<HashSet<Guid>> EtapasComAvaliacaoAsync(IReadOnlyList<FasesNoEnvelope.Fase> fases, CancellationToken cancellationToken)
    {
        List<Guid> etapas = [.. fases.SelectMany(static f => f.Etapas)];
        return [.. await _context.AvaliacoesDaEtapa
            .AsNoTracking()
            .Where(a => etapas.Contains(a.EtapaProcessoId))
            .Select(a => a.EtapaProcessoId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false)];
    }

    private static bool DoLugar(string item, string lugar) => item.StartsWith(lugar + "|", StringComparison.Ordinal);

    private Task<List<string>> ItensComAtoRegistradoAsync(Guid processoId, CancellationToken cancellationToken) =>
        _context.AtosDoProcesso
            .AsNoTracking()
            .Where(a => a.ProcessoSeletivoId == processoId && a.ItemDoEdital != null)
            .Select(a => a.ItemDoEdital!)
            .Distinct()
            .ToListAsync(cancellationToken);
}
