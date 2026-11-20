namespace Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Lê o que o membro do enum declarou em <see cref="CodigoDeContratoAttribute"/>.</summary>
public static class CodigoDeContratoExtensions
{
    /// <summary>O código que circula fora do processo.</summary>
    /// <exception cref="ArgumentOutOfRangeException">O valor não é membro do enum, como num cast de inteiro.</exception>
    /// <exception cref="CodigosDeContratoInvalidosException">O enum é inválido.</exception>
    public static string Codigo<TEnum>(this TEnum valor)
        where TEnum : struct, Enum =>
        CatalogoDe<TEnum>.Declarado(valor).Codigo;

    /// <summary>O texto que a pessoa lê.</summary>
    /// <exception cref="ArgumentOutOfRangeException">O valor não é membro do enum, como num cast de inteiro.</exception>
    /// <exception cref="CodigosDeContratoInvalidosException">O enum é inválido.</exception>
    public static string Rotulo<TEnum>(this TEnum valor)
        where TEnum : struct, Enum =>
        CatalogoDe<TEnum>.Declarado(valor).Rotulo;

    /// <summary>O que o valor significa; nulo quando o membro não declarou descrição.</summary>
    /// <exception cref="ArgumentOutOfRangeException">O valor não é membro do enum, como num cast de inteiro.</exception>
    /// <exception cref="CodigosDeContratoInvalidosException">O enum é inválido.</exception>
    public static string? Descricao<TEnum>(this TEnum valor)
        where TEnum : struct, Enum =>
        CatalogoDe<TEnum>.Declarado(valor).Descricao;
}
