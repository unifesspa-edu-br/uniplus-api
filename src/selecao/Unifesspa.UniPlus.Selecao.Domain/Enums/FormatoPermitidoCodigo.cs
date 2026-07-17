namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento único entre <see cref="FormatoPermitido"/> e o código textual canônico
/// UPPER_SNAKE do wire de comando — mesma convenção de <see cref="OperadorCodigo"/>.
/// </summary>
public static class FormatoPermitidoCodigo
{
    public const string Pdf = "PDF";
    public const string Jpeg = "JPEG";
    public const string Png = "PNG";

    public static string ToCodigo(this FormatoPermitido formato) => formato switch
    {
        FormatoPermitido.Pdf => Pdf,
        FormatoPermitido.Jpeg => Jpeg,
        FormatoPermitido.Png => Png,
        FormatoPermitido.Nenhum => throw new ArgumentOutOfRangeException(
            nameof(formato), formato, "FormatoPermitido.Nenhum é sentinela e não tem código canônico."),
        _ => throw new ArgumentOutOfRangeException(nameof(formato), formato, "FormatoPermitido desconhecido."),
    };

    /// <summary>
    /// <see langword="null"/> tanto para código ausente quanto para código fora do
    /// domínio — quem chama decide se a ausência é aceitável (campo opcional) ou se um
    /// código desconhecido deve virar erro de validação explícito (não decidido aqui,
    /// que é puro).
    /// </summary>
    public static FormatoPermitido? FromCodigo(string? codigo) => codigo switch
    {
        Pdf => FormatoPermitido.Pdf,
        Jpeg => FormatoPermitido.Jpeg,
        Png => FormatoPermitido.Png,
        _ => null,
    };
}
