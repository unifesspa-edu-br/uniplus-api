namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Entities;

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
}
