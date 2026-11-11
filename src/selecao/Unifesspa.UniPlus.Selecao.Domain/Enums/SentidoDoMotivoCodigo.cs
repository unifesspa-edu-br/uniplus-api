namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento único entre <see cref="SentidoDoMotivo"/> e o código textual canônico do wire.
/// </summary>
public static class SentidoDoMotivoCodigo
{
    public const string Favoravel = "FAVORAVEL";
    public const string Desfavoravel = "DESFAVORAVEL";

    /// <summary>Códigos aceitos no wire, na ordem canônica — para mensagem de erro e conferência.</summary>
    public static IReadOnlyList<string> Todos { get; } = [Favoravel, Desfavoravel];

    public static string ToCodigo(this SentidoDoMotivo sentido) => sentido switch
    {
        SentidoDoMotivo.Favoravel => Favoravel,
        SentidoDoMotivo.Desfavoravel => Desfavoravel,
        _ => throw new ArgumentOutOfRangeException(nameof(sentido), sentido, "SentidoDoMotivo sem código canônico."),
    };

    /// <summary>Rótulo em português, para compor mensagens.</summary>
    public static string Rotulo(this SentidoDoMotivo sentido) => sentido switch
    {
        SentidoDoMotivo.Favoravel => "favorável",
        SentidoDoMotivo.Desfavoravel => "desfavorável",
        _ => throw new ArgumentOutOfRangeException(nameof(sentido), sentido, "SentidoDoMotivo sem rótulo."),
    };

    /// <summary>
    /// Converte o código do wire. Ausente ou desconhecido vira
    /// <see cref="SentidoDoMotivo.Nenhum"/>, que quem chama trata como recusa.
    /// </summary>
    public static SentidoDoMotivo FromCodigo(string? codigo) => codigo switch
    {
        Favoravel => SentidoDoMotivo.Favoravel,
        Desfavoravel => SentidoDoMotivo.Desfavoravel,
        _ => SentidoDoMotivo.Nenhum,
    };
}
