namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento único entre <see cref="ReferenciaTipo"/> e o código textual canônico
/// UPPER_SNAKE do wire de comando (mesma convenção de <see cref="OperadorCodigo"/>) —
/// fonte de verdade única do wire format.
/// </summary>
public static class ReferenciaTipoCodigo
{
    public const string FimInscricao = "FIM_INSCRICAO";
    public const string InicioFase = "INICIO_FASE";
    public const string FimFase = "FIM_FASE";
    public const string DataEspecifica = "DATA_ESPECIFICA";

    public static string ToCodigo(this ReferenciaTipo tipo) => tipo switch
    {
        ReferenciaTipo.FimInscricao => FimInscricao,
        ReferenciaTipo.InicioFase => InicioFase,
        ReferenciaTipo.FimFase => FimFase,
        ReferenciaTipo.DataEspecifica => DataEspecifica,
        ReferenciaTipo.Nenhuma => throw new ArgumentOutOfRangeException(
            nameof(tipo), tipo, "ReferenciaTipo.Nenhuma é sentinela e não tem código canônico."),
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "ReferenciaTipo desconhecido."),
    };

    public static ReferenciaTipo FromCodigo(string? codigo) => codigo switch
    {
        FimInscricao => ReferenciaTipo.FimInscricao,
        InicioFase => ReferenciaTipo.InicioFase,
        FimFase => ReferenciaTipo.FimFase,
        DataEspecifica => ReferenciaTipo.DataEspecifica,
        _ => ReferenciaTipo.Nenhuma,
    };
}
