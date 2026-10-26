namespace Unifesspa.UniPlus.Regras.Services;

/// <summary>
/// Os códigos das cinco áreas de avaliação do ENEM. É o único vocabulário de áreas do sistema:
/// o cadastro de pesos por área e as notas importadas do INEP referenciam estes mesmos códigos.
/// </summary>
public static class AreaDoEnem
{
    public const string Redacao = "REDACAO";
    public const string CienciasDaNatureza = "CIENCIAS_DA_NATUREZA";
    public const string CienciasHumanas = "CIENCIAS_HUMANAS";
    public const string Linguagens = "LINGUAGENS";
    public const string Matematica = "MATEMATICA";

    /// <summary>As cinco áreas, na ordem canônica de exibição.</summary>
    public static IReadOnlyList<string> Todas { get; } =
    [
        Redacao,
        CienciasDaNatureza,
        CienciasHumanas,
        Linguagens,
        Matematica,
    ];
}
