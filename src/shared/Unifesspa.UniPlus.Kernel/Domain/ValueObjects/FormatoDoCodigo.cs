namespace Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using System.Text.RegularExpressions;

/// <summary>Formato compartilhado dos códigos de cadastro e de contrato.</summary>
public static partial class FormatoDoCodigo
{
    public const int ComprimentoMaximo = 80;
    public const string ExpressaoNoBanco = "^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$";

    public static bool TemFormatoValido(string valor)
    {
        ArgumentNullException.ThrowIfNull(valor);
        return FormatoValido().IsMatch(valor);
    }

    [GeneratedRegex(@"\A[A-Z][A-Z0-9]*(_[A-Z0-9]+)*\z")]
    private static partial Regex FormatoValido();
}
