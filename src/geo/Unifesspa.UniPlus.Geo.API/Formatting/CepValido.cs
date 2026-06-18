namespace Unifesspa.UniPlus.Geo.API.Formatting;

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

/// <summary>
/// Decode/normalização de CEP no boundary HTTP (ADR-0031): remove a máscara
/// (hífen e espaços) e exige exatamente 8 dígitos ASCII. O handler recebe o CEP já
/// normalizado — nunca a string crua. Formato inválido → 400 no controller.
/// </summary>
internal static partial class CepValido
{
    /// <summary>
    /// Tenta normalizar <paramref name="cep"/> para 8 dígitos. Aceita <strong>apenas</strong>
    /// os formatos canônicos: <c>01001000</c>, <c>01001-000</c> ou <c>01001 000</c>
    /// (separador único entre os 5 e os 3 dígitos). Rejeita ≠8 dígitos, não-numérico
    /// ou separadores fora de posição (ex.: <c>0-1-0-0-1-000</c>). Retorna
    /// <see langword="true"/> + <paramref name="normalizado"/> quando válido.
    /// </summary>
    public static bool TentarNormalizar(string? cep, [NotNullWhen(true)] out string? normalizado)
    {
        normalizado = null;

        if (string.IsNullOrWhiteSpace(cep))
        {
            return false;
        }

        // Valida o formato CRU (já aparado) contra os padrões canônicos — não remove
        // separadores antes, senão "0-1-0-0-1-000" passaria como se fosse 8 dígitos.
        string bruto = cep.Trim();
        if (!CepRegex().IsMatch(bruto))
        {
            return false;
        }

        normalizado = bruto
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);
        return true;
    }

    // [0-9] e não \d: em .NET \d casa dígitos Unicode (ex.: árabe-índicos); o CEP é
    // ASCII. Aceita 8 dígitos OU NNNNN-NNN OU NNNNN␠NNN — o separador só na posição
    // 5→3, nunca interno.
    [GeneratedRegex(@"^[0-9]{8}$|^[0-9]{5}[- ][0-9]{3}$")]
    private static partial Regex CepRegex();
}
