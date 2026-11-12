namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

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
    private readonly SelecaoDbContext _context;

    public FasesIniciadasDoProcesso(SelecaoDbContext context) => _context = context;

    public async Task<FasesIniciadas> ObterAsync(Guid processoId, DateTimeOffset agora, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<VersaoDasFases> versoes = await VersoesVigentesAsync(processoId, agora, cancellationToken).ConfigureAwait(false);
        HashSet<Guid> iniciadas = InicioDasFases.Origens(versoes, agora, await FatosAsync(processoId, agora, cancellationToken).ConfigureAwait(false));

        return new FasesIniciadas([.. versoes
            .SelectMany(static v => v.Fases)
            .GroupBy(static f => f.OrigemId)
            .Where(g => iniciadas.Contains(g.Key))
            .Select(static g => KeyValuePair.Create(g.Key, g.First().Unidade))]);
    }

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

    private async Task<Fatos> FatosAsync(Guid processoId, DateTimeOffset agora, CancellationToken cancellationToken) => new(
        await AtosAsync(processoId, agora, cancellationToken).ConfigureAwait(false),
        await ExecucoesDeEtapaAsync(processoId, cancellationToken).ConfigureAwait(false),
        await InscricoesAsync(processoId, cancellationToken).ConfigureAwait(false));

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

    private async Task<IReadOnlyList<(string Item, DateTimeOffset Instante)>> AtosAsync(Guid processoId, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        var atos = await _context.AtosDoProcesso
            .AsNoTracking()
            .Where(a => a.ProcessoSeletivoId == processoId && a.ItemDoEdital != null)
            .Select(a => new { Item = a.ItemDoEdital!, a.RegistradoEm })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. atos.Select(a => (a.Item, a.RegistradoEm ?? agora))];
    }
}
