namespace Unifesspa.UniPlus.Kernel.Extensions;

public static class StringExtensions
{
    public static string ApenasDigitos(this string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? string.Empty : new string(valor.Where(char.IsDigit).ToArray());

    /// <summary>
    /// Texto opcional em branco é ausência: devolve <see langword="null"/> para nulo, vazio ou só
    /// espaços, e o texto aparado nos demais casos.
    /// </summary>
    public static string? EmBrancoViraNulo(this string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
