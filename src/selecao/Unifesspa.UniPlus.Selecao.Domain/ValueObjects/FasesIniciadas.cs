namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Entities;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// As fases do cronograma publicado que já começaram, com a unidade que cada uma tem na versão publicada, pela
/// identidade estável da fase (a fase canônica de origem). É o fato de que a retificação precisa para recusar a
/// troca da unidade de uma fase em andamento.
/// </summary>
public sealed record FasesIniciadas
{
    private readonly Dictionary<Guid, Guid?> _unidadesPublicadas;

    /// <summary>Cria o conjunto a partir da fase canônica de origem e da unidade publicada de cada fase iniciada.</summary>
    public FasesIniciadas(IEnumerable<KeyValuePair<Guid, Guid?>> unidadesPublicadas)
    {
        ArgumentNullException.ThrowIfNull(unidadesPublicadas);

        _unidadesPublicadas = new(unidadesPublicadas);
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
}
