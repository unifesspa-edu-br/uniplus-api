namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static InicioDasFases;

/// <summary>
/// Quais fases já começaram, com a unidade que cada uma tem na versão publicada vigente. Lê as versões já vigentes
/// (a que ainda não vige não conta como evidência) e os fatos do processo com o instante deles; a regra de atribuição
/// está em <see cref="InicioDasFases"/>. A unidade é a da versão mais vigente em que a fase aparece (UNI-REQ-0332).
/// </summary>
internal sealed class FasesIniciadasDoProcesso : IFasesIniciadasDoProcesso
{
    private readonly UniPlusDbContext _context;

    public FasesIniciadasDoProcesso(UniPlusDbContext context) => _context = context;

    public async Task<FasesIniciadas> ObterAsync(Guid processoId, DateTimeOffset agora, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<VersaoDasFases> versoes = await VersoesVigentesAsync(processoId, agora, cancellationToken).ConfigureAwait(false);
        Fatos fatos = await FatosAsync(processoId, cancellationToken).ConfigureAwait(false);
        HashSet<Guid> iniciadas = InicioDasFases.Origens(versoes, agora, fatos);

        return new FasesIniciadas(
            UnidadesPublicadas(versoes, iniciadas), CodigosNaVersaoVigente(versoes, iniciadas), EtapasIniciadas(versoes, InicioDasFases.Etapas(versoes, agora, fatos)));
    }

    private static IEnumerable<KeyValuePair<Guid, Guid?>> UnidadesPublicadas(IReadOnlyList<VersaoDasFases> versoes, HashSet<Guid> iniciadas) =>
        versoes
            .SelectMany(static v => v.Fases)
            .GroupBy(static f => f.OrigemId)
            .Where(g => iniciadas.Contains(g.Key))
            .Select(static g => KeyValuePair.Create(g.Key, g.First().Unidade));

    private static IEnumerable<KeyValuePair<Guid, string>> CodigosNaVersaoVigente(IReadOnlyList<VersaoDasFases> versoes, HashSet<Guid> iniciadas) =>
        (versoes.Count > 0 ? versoes[0].Fases : [])
            .Where(f => iniciadas.Contains(f.OrigemId))
            .Select(static f => KeyValuePair.Create(f.OrigemId, f.Codigo));

    // A etapa se identifica pelo id, estável entre as versões; o nome é o da versão mais vigente em que ela aparece.
    private static IEnumerable<EtapaIniciada> EtapasIniciadas(IReadOnlyList<VersaoDasFases> versoes, HashSet<Guid> iniciadas) =>
        versoes
            .SelectMany(static v => v.Fases)
            .SelectMany(static f => f.Etapas)
            .Where(e => iniciadas.Contains(e.Id))
            .GroupBy(static e => e.Id)
            .Select(g => new EtapaIniciada(g.Key, g.First().Nome, versoes.Count > 0 && versoes[0].Fases.Any(f => f.Etapas.Any(e => e.Id == g.Key))));

    private async Task<IReadOnlyList<VersaoDasFases>> VersoesVigentesAsync(Guid processoId, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        var versoes = await _context.Vigentes(processoId, agora)
            .AsNoTracking()
            .Select(v => new { v.Id, v.NumeroVersao, v.VigenteAPartirDe, v.ConfiguracaoCongelada })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. versoes.Select(v => new VersaoDasFases(
            v.Id,
            v.VigenteAPartirDe,
            versoes.FirstOrDefault(s => s.NumeroVersao == v.NumeroVersao + 1)?.VigenteAPartirDe,
            FasesNoEnvelope.Das(v.ConfiguracaoCongelada)))];
    }

    private async Task<Fatos> FatosAsync(Guid processoId, CancellationToken cancellationToken) => new(
        await AtosAsync(processoId, cancellationToken).ConfigureAwait(false),
        await ExecucoesDeEtapaAsync(processoId, cancellationToken).ConfigureAwait(false),
        await InscricoesAsync(processoId, cancellationToken).ConfigureAwait(false),
        await EnviosAoInepAsync(processoId, cancellationToken).ConfigureAwait(false));

    private async Task<IReadOnlyList<EnvioAoInepDaEtapa>> EnviosAoInepAsync(Guid processoId, CancellationToken cancellationToken)
    {
        var envios = await _context.GeracoesDeEnvioAoInep
            .AsNoTracking()
            .Where(g => g.ProcessoSeletivoId == processoId)
            .Select(g => new { g.EtapaProcessoId, g.GeradoEm })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. envios.Select(static g => new EnvioAoInepDaEtapa(g.EtapaProcessoId, g.GeradoEm))];
    }

    // A inscrição recebida é a que saiu do rascunho; o instante é o da criação, o único que o registro guarda de forma estável.
    private async Task<IReadOnlyList<DateTimeOffset>> InscricoesAsync(Guid processoId, CancellationToken cancellationToken) =>
        await _context.Inscricoes
            .AsNoTracking()
            .Where(i => i.ProcessoSeletivoId == processoId && i.Situacao != SituacaoInscricao.Rascunho)
            .Select(i => i.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    // A importação das notas do ENEM numa etapa é execução dela: a avaliação nasce no lote, que guarda a versão e o instante da importação.
    private async Task<IReadOnlyList<ImportacaoNaEtapa>> ExecucoesDeEtapaAsync(Guid processoId, CancellationToken cancellationToken)
    {
        var lotes = await _context.LotesDeNotasDoEnem
            .AsNoTracking()
            .Where(l => l.ProcessoSeletivoId == processoId)
            .Select(l => new { l.EtapaProcessoId, l.VersaoConfiguracaoId, l.ImportadoEm })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. lotes.Select(static l => new ImportacaoNaEtapa(l.EtapaProcessoId, l.VersaoConfiguracaoId, l.ImportadoEm))];
    }

    private async Task<IReadOnlyList<(string Item, DateTimeOffset Instante)>> AtosAsync(Guid processoId, CancellationToken cancellationToken)
    {
        var atos = await _context.AtosDoProcesso
            .AsNoTracking()
            .Where(a => a.ProcessoSeletivoId == processoId && a.ItemDoEdital != null)
            .Select(a => new { Item = a.ItemDoEdital!, a.PublicadoEm })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. atos.Select(static a => (a.Item, a.PublicadoEm))];
    }
}
