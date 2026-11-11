namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As fases do cronograma publicado que já começaram, pela identidade estável da fase (a fase canônica de origem).
/// É o fato de que a retificação precisa para recusar a troca da unidade de uma fase em andamento.
/// </summary>
public sealed record FasesIniciadas
{
    private readonly HashSet<Guid> _origens;

    /// <summary>Cria o conjunto a partir das fases canônicas de origem das fases iniciadas.</summary>
    public FasesIniciadas(IEnumerable<Guid> origensIniciadas)
    {
        ArgumentNullException.ThrowIfNull(origensIniciadas);

        _origens = [.. origensIniciadas];
    }

    /// <summary>Nenhuma fase iniciada: o rascunho, que nunca foi publicado.</summary>
    public static FasesIniciadas Nenhuma { get; } = new([]);

    /// <summary>Se a fase de origem informada já começou.</summary>
    public bool Contem(Guid faseCanonicaOrigemId) => _origens.Contains(faseCanonicaOrigemId);
}
