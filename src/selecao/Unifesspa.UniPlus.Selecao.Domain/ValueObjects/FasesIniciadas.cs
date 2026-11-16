namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Entities;

using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>Uma etapa que já começou, pela identidade dela, e se a versão publicada vigente ainda a traz.</summary>
public sealed record EtapaIniciada(Guid Id, string Nome, bool NaVersaoVigente);

/// <summary>
/// As fases do cronograma publicado que já começaram, com a unidade que cada uma tem na versão publicada, pela
/// identidade estável da fase (a fase canônica de origem), e as que ainda estão no cronograma da versão publicada
/// vigente, e as etapas que já começaram. É o fato de que a retificação precisa para recusar a troca da unidade e a
/// remoção de uma fase ou de uma etapa em andamento.
/// </summary>
public sealed record FasesIniciadas
{
    private readonly Dictionary<Guid, Guid?> _unidadesPublicadas;
    private readonly Dictionary<Guid, string> _codigosNaVersaoVigente;
    private readonly Dictionary<Guid, EtapaIniciada> _etapas;
    private readonly HashSet<Guid> _fasesProdutoras;

    /// <summary>
    /// Cria o conjunto a partir da fase canônica de origem e da unidade publicada de cada fase iniciada, e do código das
    /// fases iniciadas que a versão publicada vigente ainda traz no cronograma: só essas podem ser removidas. As etapas
    /// iniciadas seguem a mesma regra. As fases produtoras são as que começaram por algo que pertence à própria fase: a
    /// janela dela, um ato dela ou uma decisão no lugar dela.
    /// </summary>
    public FasesIniciadas(
        IEnumerable<KeyValuePair<Guid, Guid?>> unidadesPublicadas,
        IEnumerable<KeyValuePair<Guid, string>>? codigosNaVersaoVigente = null,
        IEnumerable<EtapaIniciada>? etapas = null,
        IEnumerable<Guid>? fasesProdutoras = null)
    {
        ArgumentNullException.ThrowIfNull(unidadesPublicadas);

        _unidadesPublicadas = new(unidadesPublicadas);
        _codigosNaVersaoVigente = new(codigosNaVersaoVigente ?? []);
        _etapas = (etapas ?? []).ToDictionary(static e => e.Id);
        _fasesProdutoras = [.. fasesProdutoras ?? []];
    }

    /// <summary>Nenhuma fase iniciada: o rascunho, que nunca foi publicado.</summary>
    public static FasesIniciadas Nenhuma { get; } = new([]);

    /// <summary>Se a etapa já começou: a janela dela abriu, ela tem ato ou registro próprio, ou já terminou. A abertura de uma etapa não inicia as outras da fase.</summary>
    public bool ContemEtapa(Guid etapaId) => _etapas.ContainsKey(etapaId);

    /// <summary>
    /// Se a avaliação no lugar já começou: a fase, só quando ela mesma produziu um início; a etapa, como em
    /// <see cref="ContemEtapa"/>. A janela de uma etapa e a inscrição recebida não iniciam a avaliação da fase.
    /// </summary>
    public bool AvaliacaoComecou(LugarDaDecisao lugar)
    {
        ArgumentNullException.ThrowIfNull(lugar);

        return lugar.FaseCanonicaOrigemId is { } fase
            ? _fasesProdutoras.Contains(fase)
            : lugar.EtapaProcessoId is { } etapa && ContemEtapa(etapa);
    }

    /// <summary>Se a fase de origem informada já começou.</summary>
    public bool Contem(Guid faseCanonicaOrigemId) => _unidadesPublicadas.ContainsKey(faseCanonicaOrigemId);

    /// <summary>Se a fase já começou e a unidade dela não é a publicada.</summary>
    public bool UnidadeTrocada(FaseCronograma fase)
    {
        ArgumentNullException.ThrowIfNull(fase);

        return _unidadesPublicadas.TryGetValue(fase.FaseCanonicaOrigemId, out Guid? publicada)
            && publicada != fase.UnidadeResponsavelOrigemId;
    }

    /// <summary>As fases, entre as informadas, que já começaram e cuja unidade não é a publicada.</summary>
    public IEnumerable<FaseCronograma> FasesComUnidadeTrocada(IEnumerable<FaseCronograma> fases)
    {
        ArgumentNullException.ThrowIfNull(fases);

        return fases.Where(UnidadeTrocada);
    }

    /// <summary>
    /// O código das fases iniciadas que a versão publicada vigente ainda traz e que não estão entre as informadas. A
    /// fase que já saiu de uma versão anterior não conta como removida agora.
    /// </summary>
    public IEnumerable<string> CodigosDasFasesRemovidas(IEnumerable<FaseCronograma> fases)
    {
        ArgumentNullException.ThrowIfNull(fases);

        HashSet<Guid> restantes = [.. fases.Select(static f => f.FaseCanonicaOrigemId)];

        return _codigosNaVersaoVigente.Where(f => !restantes.Contains(f.Key)).Select(static f => f.Value);
    }

    /// <summary>As etapas iniciadas que a versão publicada vigente ainda traz e que não estão entre as informadas.</summary>
    public IEnumerable<EtapaIniciada> EtapasRemovidas(IEnumerable<EtapaProcesso> etapas)
    {
        ArgumentNullException.ThrowIfNull(etapas);

        HashSet<Guid> restantes = [.. etapas.Select(static e => e.Id)];

        return _etapas.Values.Where(e => e.NaVersaoVigente && !restantes.Contains(e.Id));
    }
}
