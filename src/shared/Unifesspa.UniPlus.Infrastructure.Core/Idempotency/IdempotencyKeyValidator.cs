namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

using System.Text.RegularExpressions;

/// <summary>
/// Validação de formato do header <c>Idempotency-Key</c> conforme
/// draft-ietf-httpapi-idempotency-key-header-07 (ADR-0027).
/// </summary>
/// <remarks>
/// Vive fora do <see cref="IdempotencyFilter{TDbContext}"/> porque o source
/// generator de <see cref="GeneratedRegexAttribute"/> não suporta métodos em
/// tipos genéricos — manter a regex aqui, numa classe estática não-genérica,
/// preserva a geração em tempo de compilação.
/// </remarks>
internal static partial class IdempotencyKeyValidator
{
    [GeneratedRegex(@"^[\x21-\x7E]{1,255}$")]
    private static partial Regex KeyPrintableAsciiRegex();

    // Vírgula e ponto-vírgula são proibidos pelo draft IETF mesmo estando no
    // range ASCII printable — usados como separadores em sf-list. Espaço já é
    // rejeitado pela regex (0x20 < 0x21).
    private static readonly char[] ForbiddenKeyChars = [',', ';'];

    /// <summary>
    /// <c>true</c> quando a chave está em <c>[\x21-\x7E]{1,255}</c> e não contém
    /// vírgula nem ponto-vírgula.
    /// </summary>
    public static bool IsValid(string key)
    {
        // Regex {1,255} já garante length em range e [\x21-\x7E] já exclui
        // espaço (0x20), tab (0x09) e demais caracteres não-imprimíveis.
        // ForbiddenKeyChars cobre apenas vírgula (0x2C) e ponto-vírgula (0x3B)
        // que estão dentro do range printable mas são proibidos pelo
        // draft IETF (caracteres usados como separadores em sf-list).
        if (key.IndexOfAny(ForbiddenKeyChars) >= 0)
        {
            return false;
        }

        return KeyPrintableAsciiRegex().IsMatch(key);
    }
}
