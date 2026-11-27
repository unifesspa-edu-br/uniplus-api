namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Quais fases já começaram, com a unidade que cada uma tem na versão publicada vigente. Só lê: as versões já vigentes
/// (a que ainda não vige não conta como evidência) e os fatos do processo com o instante deles; quem decide quais fases
/// começaram é <see cref="InicioDasFases"/>. A unidade é a da versão mais vigente em que a fase aparece (UNI-REQ-0332).
/// </summary>
internal sealed class FasesIniciadasDoProcesso : IFasesIniciadasDoProcesso
{
    private readonly UniPlusDbContext _context;
    private readonly TimeProvider _relogio;
    private readonly IReadOnlyList<IFonteDaAvaliacaoIniciadaNoLugar> _fontes;

    public FasesIniciadasDoProcesso(UniPlusDbContext context, TimeProvider relogio, IEnumerable<IFonteDaAvaliacaoIniciadaNoLugar> fontes)
    {
        _context = context;
        _relogio = relogio;
        _fontes = [.. fontes];
    }

    public async Task<FasesIniciadasNoInstante> ObterAgoraAsync(Guid processoId, CancellationToken cancellationToken = default)
    {
        DateTimeOffset agora = _relogio.GetUtcNow();
        return new FasesIniciadasNoInstante(await ObterAsync(processoId, agora, cancellationToken).ConfigureAwait(false), agora);
    }

    public async Task<FasesIniciadas> ObterAsync(Guid processoId, DateTimeOffset agora, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FasesDaVersaoPublicada> versoes = await VersoesVigentesAsync(processoId, agora, cancellationToken).ConfigureAwait(false);
        InicioDasFases inicio = new(versoes, await FatosAsync(processoId, cancellationToken).ConfigureAwait(false));
        HashSet<Guid> iniciadas = inicio.FasesIniciadas(agora);

        return new FasesIniciadas(
            UnidadesPublicadas(versoes, iniciadas),
            CitacoesNaVersaoVigente(versoes, iniciadas),
            EtapasIniciadas(versoes, inicio.EtapasIniciadas(agora)),
            inicio.FasesProdutoras(agora));
    }

    private static IEnumerable<KeyValuePair<Guid, Guid?>> UnidadesPublicadas(IReadOnlyList<FasesDaVersaoPublicada> versoes, HashSet<Guid> iniciadas) =>
        versoes
            .SelectMany(static v => v.Fases)
            .GroupBy(static f => f.FaseCanonicaOrigemId)
            .Where(g => iniciadas.Contains(g.Key))
            .Select(static g => KeyValuePair.Create(g.Key, g.First().UnidadeResponsavelOrigemId));

    private static IEnumerable<KeyValuePair<Guid, CitacaoDaFase>> CitacoesNaVersaoVigente(IReadOnlyList<FasesDaVersaoPublicada> versoes, HashSet<Guid> iniciadas) =>
        (versoes.Count > 0 ? versoes[0].Fases : [])
            .Where(f => iniciadas.Contains(f.FaseCanonicaOrigemId))
            .Select(static f => KeyValuePair.Create(f.FaseCanonicaOrigemId, f.Citacao));

    // A etapa se identifica pelo id, estável entre as versões; o nome é o da versão mais vigente em que ela aparece.
    private static IEnumerable<EtapaIniciada> EtapasIniciadas(IReadOnlyList<FasesDaVersaoPublicada> versoes, HashSet<Guid> iniciadas) =>
        versoes
            .SelectMany(static v => v.Fases)
            .SelectMany(static f => f.Etapas)
            .Where(e => iniciadas.Contains(e.Id))
            .GroupBy(static e => e.Id)
            .Select(g => new EtapaIniciada(g.Key, g.First().Nome, versoes.Count > 0 && versoes[0].Fases.Any(f => f.Etapas.Any(e => e.Id == g.Key))));

    private async Task<IReadOnlyList<FasesDaVersaoPublicada>> VersoesVigentesAsync(Guid processoId, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        var versoes = await _context.Vigentes(processoId, agora)
            .AsNoTracking()
            .Select(v => new { v.Id, v.NumeroVersao, v.VigenteAPartirDe, v.ConfiguracaoCongelada })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. versoes.Select(v => new FasesDaVersaoPublicada(
            v.Id,
            v.VigenteAPartirDe,
            versoes.FirstOrDefault(s => s.NumeroVersao == v.NumeroVersao + 1)?.VigenteAPartirDe,
            FasesNoEnvelope.Das(v.ConfiguracaoCongelada)))];
    }

    private async Task<FatosDoInicioDasFases> FatosAsync(Guid processoId, CancellationToken cancellationToken) => new(
        await AtosAsync(processoId, cancellationToken).ConfigureAwait(false),
        await ExecucoesDeEtapaAsync(processoId, cancellationToken).ConfigureAwait(false),
        await InscricoesAsync(processoId, cancellationToken).ConfigureAwait(false),
        await EnviosAoInepAsync(processoId, cancellationToken).ConfigureAwait(false),
        await AvaliacoesIniciadasAsync(processoId, cancellationToken).ConfigureAwait(false));

    private async Task<IReadOnlyList<AvaliacaoIniciadaNoLugar>> AvaliacoesIniciadasAsync(Guid processoId, CancellationToken cancellationToken)
    {
        List<AvaliacaoIniciadaNoLugar> avaliacoes = [];

        foreach (IFonteDaAvaliacaoIniciadaNoLugar fonte in _fontes)
        {
            avaliacoes.AddRange(await fonte.ListarAsync(processoId, cancellationToken).ConfigureAwait(false));
        }

        return avaliacoes;
    }

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
    private async Task<IReadOnlyList<ImportacaoDeNotasNaEtapa>> ExecucoesDeEtapaAsync(Guid processoId, CancellationToken cancellationToken)
    {
        var importacoes = await _context.LotesDeNotasDoEnem
            .AsNoTracking()
            .Where(l => l.ProcessoSeletivoId == processoId)
            .Select(l => new { l.EtapaProcessoId, l.VersaoConfiguracaoId, l.ImportadoEm })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. importacoes.Select(static i => new ImportacaoDeNotasNaEtapa(i.EtapaProcessoId, i.VersaoConfiguracaoId, i.ImportadoEm))];
    }

    // O ato conta como início do lugar pelo instante da divulgação, gravado uma só vez no registro: nenhum estado
    // posterior do ato retira o fato nem muda o instante. A chave que não declara um lugar reconhecível é descartada.
    private async Task<IReadOnlyList<AtoPublicadoNoLugar>> AtosAsync(Guid processoId, CancellationToken cancellationToken)
    {
        var atos = await _context.AtosDoProcesso
            .AsNoTracking()
            .Where(a => a.ProcessoSeletivoId == processoId && a.ItemDoEdital != null)
            .Select(a => new { Item = a.ItemDoEdital!, a.PublicadoEm })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. atos.Select(static a => AtoNoLugarDaChave(a.Item, a.PublicadoEm)).OfType<AtoPublicadoNoLugar>()];
    }

    private static AtoPublicadoNoLugar? AtoNoLugarDaChave(string chave, DateTimeOffset publicadoEm) =>
        IdentidadeDoItemDoEdital.LugarGravadoNaChave(chave) is { } lugar
            ? new AtoPublicadoNoLugar(lugar.FaseCanonicaOrigemId, lugar.EtapaProcessoId, publicadoEm)
            : null;
}
