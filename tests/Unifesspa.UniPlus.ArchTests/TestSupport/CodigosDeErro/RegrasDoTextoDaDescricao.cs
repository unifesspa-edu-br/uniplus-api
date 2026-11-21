namespace Unifesspa.UniPlus.ArchTests.TestSupport.CodigosDeErro;

using System.Text.RegularExpressions;

internal static partial class RegrasDoTextoDaDescricao
{
    internal static IEnumerable<string> Conferir(string texto)
    {
        if (ApelidoAntigo().IsMatch(texto))
            yield return "apelido antigo de requisito";
        if (CpfFormatado().IsMatch(texto) || CpfSemPontuacao().IsMatch(texto))
            yield return "CPF";
        if (DigitosConsecutivos().IsMatch(texto))
            yield return "sequência de 11 ou mais dígitos";
        if (Email().IsMatch(texto))
            yield return "e-mail";
        if (CaixaDeAviso().IsMatch(texto))
            yield return "caixa de aviso";
    }

    [GeneratedRegex(@"\AUNI-REQ-[0-9]{4}\z", RegexOptions.CultureInvariant)]
    internal static partial Regex Requisito();

    [GeneratedRegex(@"\Auniplus(?:\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant)]
    internal static partial Regex CodigoPublico();

    [GeneratedRegex(@"\bRN[0-9]+\b|(?<!UNI-)\bREQ-[0-9]+\b|\bUNI-REQ-(?![0-9]{4}\b)[0-9]+", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ApelidoAntigo();

    [GeneratedRegex(@"(?<!\d)\d{3}\.\d{3}\.\d{3}-\d{2}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex CpfFormatado();

    [GeneratedRegex(@"(?<!\d)\d{11}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex CpfSemPontuacao();

    [GeneratedRegex(@"\d{11,}", RegexOptions.CultureInvariant)]
    private static partial Regex DigitosConsecutivos();

    [GeneratedRegex(@"[\w.!#$%&'*+/=?^\x60{|}~-]+@(?:[\w-]+(?:\.[\w-]+)+|\[[^\]\s]+\])", RegexOptions.CultureInvariant)]
    private static partial Regex Email();

    [GeneratedRegex(@"^\s*:::", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex CaixaDeAviso();
}
