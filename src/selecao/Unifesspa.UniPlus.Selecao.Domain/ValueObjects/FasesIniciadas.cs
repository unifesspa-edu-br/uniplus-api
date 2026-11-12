namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Entities;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// As fases do cronograma publicado que já começaram, com a unidade que cada uma tem na versão publicada, pela
/// identidade estável da fase (a fase canônica de origem), e as que ainda estão no cronograma da versão publicada
/// vigente. É o fato de que a retificação precisa para recusar a troca da unidade e a remoção de uma fase em andamento.
/// </summary>
public sealed record FasesIniciadas
{
    private readonly Dictionary<Guid, Guid?> _unidadesPublicadas;
    private readonly Dictionary<Guid, string> _codigosNaVersaoVigente;

    /// <summary>
    /// Cria o conjunto a partir da fase canônica de origem e da unidade publicada de cada fase iniciada, e do código das
    /// fases iniciadas que a versão publicada vigente ainda traz no cronograma: só essas podem ser removidas.
    /// </summary>
    public FasesIniciadas(
        IEnumerable<KeyValuePair<Guid, Guid?>> unidadesPublicadas,
        IEnumerable<KeyValuePair<Guid, string>>? codigosNaVersaoVigente = null)
    {
        ArgumentNullException.ThrowIfNull(unidadesPublicadas);

        _unidadesPublicadas = new(unidadesPublicadas);
        _codigosNaVersaoVigente = new(codigosNaVersaoVigente ?? []);
    }

    /// <summary>Nenhuma fase iniciada: o rascunho, que nunca foi publicado.</summary>
    public static FasesIniciadas Nenhuma { get; } = new([]);

    /// <summary>Se a fase de origem informada já começou.</summary>
    public bool Contem(Guid faseCanonicaOrigemId) => _unidadesPublicadas.ContainsKey(faseCanonicaOrigemId);

    /// <summary>Se a fase já começou e a unidade dela não é a publicada.</summary>
    public bool UnidadeTrocada(FaseCronograma fase)
    {
        ArgumentNullException.ThrowIfNull(fase);

        return _unidadesPublicadas.TryGetValue(fase.FaseCanonicaOrigemId, out Guid? publicada)
            && publicada != fase.UnidadeResponsavelOrigemId;
    }

    // A unidade que conduz a fase decide as ações dela, e a fase que já começou tem ato e registro feitos sob a
    // unidade publicada: trocá-la deixaria o que já aconteceu sem a unidade que o conduziu. A comparação é com a
    // versão publicada, e não com o rascunho anterior, para que retirar e reinserir a fase não contorne a regra.
    /// <summary>A recusa da troca de unidade numa fase iniciada, citando a primeira fase trocada; nula se não há troca.</summary>
    public DomainError? RecusaDaTroca(IEnumerable<FaseCronograma> fases)
    {
        ArgumentNullException.ThrowIfNull(fases);

        return fases.FirstOrDefault(UnidadeTrocada) is { } fase
            ? new DomainError(
                "FaseCronograma.UnidadeDeFaseIniciadaNaoMuda",
                $"A fase '{fase.Codigo}' (ordem {fase.Ordem}) já começou, e a unidade responsável por ela não muda.")
            : null;
    }

    /// <summary>A recusa de trocar a unidade ou de remover do cronograma uma fase iniciada; nula se não há nenhuma das duas.</summary>
    public DomainError? RecusaDaAlteracao(IEnumerable<FaseCronograma> fases) => RecusaDaTroca(fases) ?? RecusaDaRemocao(fases);

    // Remover a fase numa retificação e recolocá-la com outra unidade na seguinte contornaria a recusa da troca. Só conta
    // a fase que a versão publicada vigente ainda traz: a que já saiu de uma versão anterior não é removida agora.
    /// <summary>A recusa da remoção de uma fase iniciada do cronograma, citando a primeira removida; nula se não há remoção.</summary>
    public DomainError? RecusaDaRemocao(IEnumerable<FaseCronograma> fases)
    {
        ArgumentNullException.ThrowIfNull(fases);

        HashSet<Guid> restantes = [.. fases.Select(static f => f.FaseCanonicaOrigemId)];

        return _codigosNaVersaoVigente.FirstOrDefault(f => !restantes.Contains(f.Key)) is { Value: { } codigo }
            ? new DomainError(
                "FaseCronograma.FaseIniciadaNaoSaiDoCronograma",
                $"A fase '{codigo}' já começou, e ela não sai do cronograma.")
            : null;
    }
}
