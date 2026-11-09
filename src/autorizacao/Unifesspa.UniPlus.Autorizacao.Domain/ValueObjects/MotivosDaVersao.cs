namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>O motivo que acompanha a recusa, o cancelamento e a remoção: texto de até 500 caracteres.</summary>
public static class MotivosDaVersao
{
    public const int MaxLength = 500;

    /// <summary>
    /// O texto aparado, ou nulo quando está vazio, passa do limite ou traz o caractere nulo, que a coluna textual do
    /// banco não armazena.
    /// </summary>
    public static string? Normalizar(string? texto)
    {
        string? aparado = texto?.Trim();

        return string.IsNullOrEmpty(aparado) || aparado.Length > MaxLength || aparado.Contains('\0', StringComparison.Ordinal)
            ? null
            : aparado;
    }
}
