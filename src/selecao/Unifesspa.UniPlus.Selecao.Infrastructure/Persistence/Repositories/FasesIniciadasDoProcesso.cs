namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Quais fases já começaram, com a unidade que cada uma tem na versão publicada. A fase começou se a janela dela abriu
/// ou encerrou enquanto a versão que a continha vigia (antes de a sucessora vigir), se há ato do item que ela declara, avaliação lançada numa etapa dela ou
/// inscrição recebida na fase que a coleta. O que começou não "desinicia": uma retificação que adia a janela, ou que
/// retira a fase, não apaga o início (UNI-REQ-0333). A unidade é a da versão mais vigente em que a fase aparece.
/// </summary>
internal sealed class FasesIniciadasDoProcesso : IFasesIniciadasDoProcesso
{
    private readonly SelecaoDbContext _context;

    public FasesIniciadasDoProcesso(SelecaoDbContext context) => _context = context;

    public async Task<FasesIniciadas> ObterAsync(Guid processoId, DateTimeOffset agora, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FaseDaVersao> fases = await FasesPublicadasAsync(processoId, agora, cancellationToken).ConfigureAwait(false);
        Sinais sinais = await SinaisAsync(processoId, [.. fases.Select(static v => v.Fase)], cancellationToken).ConfigureAwait(false);

        return new FasesIniciadas([.. fases
            .GroupBy(static f => f.Fase.OrigemId)
            .Where(g => g.Any(f => Iniciada(f, agora, sinais)))
            .Select(static g => KeyValuePair.Create(g.Key, g.First().Fase.Unidade))]);
    }

    // A janela de uma versão, o ato, a avaliação e a inscrição são fatos do processo, e só a janela vive no envelope.
    private sealed record Sinais(List<string> ItensComAto, HashSet<Guid> EtapasComAvaliacao, bool HaInscricao);

    // A fase de uma versão com o instante em que a sucessora passou a vigir: a janela que ela declarava só vale
    // como início se abriu antes disso. Adiar a janela antes de ela abrir não faz a fase ter começado.
    private sealed record FaseDaVersao(FasesNoEnvelope.Fase Fase, DateTimeOffset? SubstituidaEm);

    // O ato guarda o lugar do item que o edital declara: a fase de origem, ou a etapa. O tipo do ato sozinho não
    // serve, porque duas fases podem declarar o mesmo tipo e o ato de uma não inicia a outra.
    private static bool Iniciada(FaseDaVersao versao, DateTimeOffset agora, Sinais sinais) =>
        JanelaAbriu(versao.Fase.Inicio, versao.SubstituidaEm, agora)
        || JanelaAbriu(versao.Fase.Fim, versao.SubstituidaEm, agora)
        || sinais.ItensComAto.Any(item => DoLugar(item, $"fase:{versao.Fase.OrigemId:D}") || versao.Fase.Etapas.Any(e => DoLugar(item, $"etapa:{e:D}")))
        || versao.Fase.Etapas.Any(sinais.EtapasComAvaliacao.Contains)
        || (versao.Fase.ColetaInscricao && sinais.HaInscricao);

    private static bool JanelaAbriu(DateTimeOffset? instante, DateTimeOffset? substituidaEm, DateTimeOffset agora) =>
        instante <= agora && (substituidaEm is null || instante < substituidaEm);

    private static bool DoLugar(string item, string lugar) => item.StartsWith(lugar + "|", StringComparison.Ordinal);

    private async Task<IReadOnlyList<FaseDaVersao>> FasesPublicadasAsync(
        Guid processoId, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        var versoes = await _context.Publicadas(processoId, agora)
            .AsNoTracking()
            .Select(v => new { v.NumeroVersao, v.VigenteAPartirDe, v.ConfiguracaoCongelada })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. versoes.SelectMany(v => FasesNoEnvelope.Das(v.ConfiguracaoCongelada)
            .Select(f => new FaseDaVersao(f, versoes.FirstOrDefault(s => s.NumeroVersao == v.NumeroVersao + 1)?.VigenteAPartirDe)))];
    }

    private async Task<Sinais> SinaisAsync(Guid processoId, IReadOnlyList<FasesNoEnvelope.Fase> fases, CancellationToken cancellationToken) => new(
        await ItensComAtoRegistradoAsync(processoId, cancellationToken).ConfigureAwait(false),
        await EtapasComAvaliacaoAsync(fases, cancellationToken).ConfigureAwait(false),
        await HaInscricaoRecebidaAsync(processoId, cancellationToken).ConfigureAwait(false));

    private Task<bool> HaInscricaoRecebidaAsync(Guid processoId, CancellationToken cancellationToken) =>
        _context.Inscricoes
            .AsNoTracking()
            .AnyAsync(i => i.ProcessoSeletivoId == processoId && i.Situacao != SituacaoInscricao.Rascunho, cancellationToken);

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

    private Task<List<string>> ItensComAtoRegistradoAsync(Guid processoId, CancellationToken cancellationToken) =>
        _context.AtosDoProcesso
            .AsNoTracking()
            .Where(a => a.ProcessoSeletivoId == processoId && a.ItemDoEdital != null)
            .Select(a => a.ItemDoEdital!)
            .Distinct()
            .ToListAsync(cancellationToken);
}
