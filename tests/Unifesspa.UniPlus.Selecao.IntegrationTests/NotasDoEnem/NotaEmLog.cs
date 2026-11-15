namespace Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem;

using System.Text.RegularExpressions;

/// <summary>
/// Reconhece uma nota do ENEM escrita num log. A nota só conta como escrita quando aparece como valor próprio, sem letra,
/// dígito ou hífen encostado: o log também traz texto aleatório (identificadores com letras e dígitos hexadecimais, como o
/// separador do envio multipart), e uma nota curta como "960" aparece por acaso dentro dele sem que nada tenha vazado.
/// </summary>
internal static class NotaEmLog
{
    public static bool Aparece(string log, string nota)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentException.ThrowIfNullOrEmpty(nota);

        return Regex.IsMatch(
            log,
            $@"(?<![\p{{L}}\p{{N}}-]){Regex.Escape(nota)}(?![\p{{L}}\p{{N}}-])",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(5));
    }
}
