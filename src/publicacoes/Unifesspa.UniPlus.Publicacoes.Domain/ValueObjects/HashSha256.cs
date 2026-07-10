namespace Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

using System.Text.RegularExpressions;

/// <summary>
/// Predicado de formato de um hash SHA-256 em hexadecimal minúsculo (64
/// caracteres). Cópia local em <c>Publicacoes.Domain</c> — o computador de hash
/// canônico vive em <c>Selecao.Domain</c> e o isolamento de módulo (R8/ADR-0056)
/// proíbe Publicações de referenciá-lo. Aqui só se valida o <b>formato</b>: o
/// hash em si é prova recebida pronta (o hash do PDF publicado, ou o hash da
/// versão de configuração que governou o ato), nunca recomputado por este módulo.
/// </summary>
public static partial class HashSha256
{
    /// <summary>Verdadeiro quando <paramref name="valor"/> é um SHA-256 hex minúsculo de 64 caracteres.</summary>
    public static bool TemFormatoValido(string? valor) =>
        valor is not null && Formato().IsMatch(valor);

    [GeneratedRegex(@"^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Formato();
}
