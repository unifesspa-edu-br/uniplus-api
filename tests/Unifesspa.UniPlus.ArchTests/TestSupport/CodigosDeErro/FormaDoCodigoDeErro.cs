namespace Unifesspa.UniPlus.ArchTests.TestSupport.CodigosDeErro;

using System.Text.RegularExpressions;

/// <summary>
/// A forma do código de erro de fio fixada na ADR-0023: segmentos separados por ponto, o primeiro só com
/// letras minúsculas e os seguintes só com letras minúsculas e sublinhado. Dígito, hífen e maiúscula
/// fogem da forma, porque o código vira nome de página de erro e identificador estável para o cliente.
/// </summary>
internal static partial class FormaDoCodigoDeErro
{
    internal static bool EstaNaForma(string codigo) => Forma().IsMatch(codigo);

    [GeneratedRegex(@"^[a-z]+(\.[a-z_]+)+\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Forma();
}
