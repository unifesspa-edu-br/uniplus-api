namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento único entre <see cref="ReferenciaTipoIdadeEmissao"/> e o código textual
/// canônico UPPER_SNAKE do wire de comando — mesma convenção de
/// <see cref="ReferenciaTipoCodigo"/> (PR-b).
/// </summary>
public static class ReferenciaTipoIdadeEmissaoCodigo
{
    public const string FimInscricao = "FIM_INSCRICAO";
    public const string InicioFase = "INICIO_FASE";
    public const string FimFase = "FIM_FASE";
    public const string DataEspecifica = "DATA_ESPECIFICA";
    public const string DataSubmissao = "DATA_SUBMISSAO";

    public static string ToCodigo(this ReferenciaTipoIdadeEmissao tipo) => tipo switch
    {
        ReferenciaTipoIdadeEmissao.FimInscricao => FimInscricao,
        ReferenciaTipoIdadeEmissao.InicioFase => InicioFase,
        ReferenciaTipoIdadeEmissao.FimFase => FimFase,
        ReferenciaTipoIdadeEmissao.DataEspecifica => DataEspecifica,
        ReferenciaTipoIdadeEmissao.DataSubmissao => DataSubmissao,
        ReferenciaTipoIdadeEmissao.Nenhuma => throw new ArgumentOutOfRangeException(
            nameof(tipo), tipo, "ReferenciaTipoIdadeEmissao.Nenhuma é sentinela e não tem código canônico."),
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "ReferenciaTipoIdadeEmissao desconhecido."),
    };

    /// <summary>
    /// <see langword="null"/> tanto para código ausente quanto para código fora do
    /// domínio — mesmo raciocínio de <see cref="UnidadeIdadeCodigo.FromCodigo"/>: a
    /// coerência tudo-nulo OU completo de <see cref="ValueObjects.IdadeMaximaEmissao"/>
    /// precisa distinguir ausência de presença; a checagem de domínio de um código NÃO
    /// NULO é do <c>FluentValidation</c> (Application).
    /// </summary>
    public static ReferenciaTipoIdadeEmissao? FromCodigo(string? codigo) => codigo switch
    {
        FimInscricao => ReferenciaTipoIdadeEmissao.FimInscricao,
        InicioFase => ReferenciaTipoIdadeEmissao.InicioFase,
        FimFase => ReferenciaTipoIdadeEmissao.FimFase,
        DataEspecifica => ReferenciaTipoIdadeEmissao.DataEspecifica,
        DataSubmissao => ReferenciaTipoIdadeEmissao.DataSubmissao,
        _ => null,
    };
}
