namespace Unifesspa.UniPlus.Kernel.Extensions;

public static class StringExtensions
{
    public static string ApenasDigitos(this string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? string.Empty : new string(valor.Where(char.IsDigit).ToArray());
}
