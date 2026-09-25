namespace Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using System.Text.RegularExpressions;

/// <summary>
/// Formato dos identificadores legíveis que aparecem em endereço público: kebab-case em
/// minúsculas, sem acento, de 3 a 64 caracteres.
/// </summary>
/// <remarks>
/// Formato: <c>^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$</c> — inicia com letra minúscula, segmentos
/// separados por hífen único (sem hífens consecutivos nem nas pontas), termina com letra
/// minúscula ou dígito. Vive no Kernel porque mais de um módulo escolhe identificadores com
/// esta mesma forma (o slug da unidade administradora e o identificador legível do processo
/// seletivo): duas cópias da expressão divergiriam em silêncio. Cada value object continua
/// dono das próprias mensagens e códigos de erro.
/// </remarks>
public static partial class FormatoKebab
{
    public const int ComprimentoMinimo = 3;
    public const int ComprimentoMaximo = 64;

    public static bool TemComprimentoValido(string valor)
    {
        ArgumentNullException.ThrowIfNull(valor);
        return valor.Length is >= ComprimentoMinimo and <= ComprimentoMaximo;
    }

    public static bool TemFormatoValido(string valor)
    {
        ArgumentNullException.ThrowIfNull(valor);
        return FormatoValido().IsMatch(valor);
    }

    [GeneratedRegex(@"^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoValido();
}
