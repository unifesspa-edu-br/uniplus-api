namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento único entre <see cref="ChaveDistincao"/> e o código textual canônico UPPER_SNAKE
/// do wire de comando — mesma convenção de <see cref="TipoAbrangenciaCodigo"/>.
/// </summary>
public static class ChaveDistincaoCodigo
{
    public const string CompetenciaMensal = "COMPETENCIA_MENSAL";
    public const string ExercicioAnual = "EXERCICIO_ANUAL";
    public const string Ocorrencia = "OCORRENCIA";

    public static string ToCodigo(this ChaveDistincao chave) => chave switch
    {
        ChaveDistincao.CompetenciaMensal => CompetenciaMensal,
        ChaveDistincao.ExercicioAnual => ExercicioAnual,
        ChaveDistincao.Ocorrencia => Ocorrencia,
        ChaveDistincao.Nenhuma => throw new ArgumentOutOfRangeException(
            nameof(chave), chave, "ChaveDistincao.Nenhuma é sentinela e não tem código canônico."),
        _ => throw new ArgumentOutOfRangeException(nameof(chave), chave, "ChaveDistincao desconhecida."),
    };

    public static ChaveDistincao FromCodigo(string? codigo) => codigo switch
    {
        CompetenciaMensal => ChaveDistincao.CompetenciaMensal,
        ExercicioAnual => ChaveDistincao.ExercicioAnual,
        Ocorrencia => ChaveDistincao.Ocorrencia,
        _ => ChaveDistincao.Nenhuma,
    };
}
