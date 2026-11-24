namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Lê o código de um enum que o pedido traz como texto. Quem lê decide a recusa: o código desconhecido vira nulo aqui, e o
/// domínio o recusa junto com as demais recusas do pedido.
/// </summary>
internal static class CodigoLidoDoPedido
{
    /// <summary>O valor do código, ou nulo quando o texto está ausente, em branco ou não pertence ao enum.</summary>
    public static TEnum? Ler<TEnum>(string? codigo)
        where TEnum : struct, Enum =>
        CatalogoDe<TEnum>.TentarObter(codigo, out TEnum valor) ? valor : null;

    /// <summary>
    /// Para o campo que pode faltar: devolve <see langword="true"/> com valor nulo quando o texto está ausente ou em
    /// branco, e <see langword="false"/> só quando há um texto que o enum não conhece, que não pode passar por ausente.
    /// </summary>
    public static bool TentarLerOpcional<TEnum>(string? codigo, out TEnum? valor)
        where TEnum : struct, Enum
    {
        valor = string.IsNullOrWhiteSpace(codigo) ? null : Ler<TEnum>(codigo);

        return valor is not null || string.IsNullOrWhiteSpace(codigo);
    }
}
