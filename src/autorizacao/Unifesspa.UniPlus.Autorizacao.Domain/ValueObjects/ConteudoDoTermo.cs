namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using System.Security.Cryptography;
using System.Text;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O texto de uma versão do termo de responsabilidade, já normalizado, o ato que a aprova e o resumo do texto. O que se
/// grava é o que se exibe e o que se resume, de modo que qualquer um recalcula o resumo do texto que recebeu.
/// </summary>
/// <remarks>
/// A normalização segue a ADR-0162: forma Unicode NFC; <c>\r\n</c> e <c>\r</c> viram <c>\n</c>; sem espaço em branco no
/// fim das linhas nem no início e no fim do texto; nenhum caractere de controle além de <c>\n</c> e <c>\t</c>. O resumo é
/// o SHA-256, em hexadecimal minúsculo, dos bytes UTF-8 sem BOM do texto normalizado. O ato fica fora do resumo: o
/// resumo prova o texto apresentado, e o ato é metadado da versão.
/// </remarks>
public sealed record ConteudoDoTermo
{
    public const int TextoMinLength = 200;
    public const int TextoMaxLength = 20_000;
    public const int AtoMaxLength = 500;
    public const int ResumoLength = 64;

    public string Texto { get; }

    public string Ato { get; }

    /// <summary>SHA-256 do texto normalizado, em hexadecimal minúsculo.</summary>
    public string Resumo { get; }

    private ConteudoDoTermo(string texto, string ato)
    {
        Texto = texto;
        Ato = ato;
        Resumo = Resumir(texto);
    }

    /// <summary>Normaliza e confere o texto e o ato, acumulando as violações por campo (ADR-0125).</summary>
    public static Result<ConteudoDoTermo> Criar(string? texto, string? ato)
    {
        string? textoNormalizado = NormalizarTexto(texto);
        string? atoNormalizado = NormalizarAto(ato);

        List<FieldError> erros = [.. ErrosDoTexto(textoNormalizado), .. ErrosDoAto(atoNormalizado)];

        return erros.Count > 0
            ? Result<ConteudoDoTermo>.ValidationFailure(erros)
            : Result<ConteudoDoTermo>.Success(new ConteudoDoTermo(textoNormalizado!, atoNormalizado!));
    }

    /// <summary>O SHA-256, em hexadecimal minúsculo, dos bytes UTF-8 sem BOM do texto.</summary>
    public static string Resumir(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));
    }

    // Nulo quando o texto, depois de normalizado, não cabe nos limites ou traz caractere proibido.
    private static string? NormalizarTexto(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto) || TemSurrogateSolto(texto))
        {
            return null;
        }

        string[] linhas = texto.Normalize(NormalizationForm.FormC).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        string normalizado = string.Join('\n', linhas.Select(static l => l.TrimEnd())).Trim();

        return normalizado.Length is >= TextoMinLength and <= TextoMaxLength && !TemControleProibido(normalizado) ? normalizado : null;
    }

    private static string? NormalizarAto(string? ato)
    {
        string? normalizado = ato?.Trim();

        return string.IsNullOrEmpty(normalizado) || normalizado.Length > AtoMaxLength || normalizado.Any(char.IsControl)
            ? null
            : normalizado;
    }

    private static IEnumerable<FieldError> ErrosDoTexto(string? texto)
    {
        if (texto is null)
        {
            yield return new("texto", new DomainError(
                TermoErrorCodes.TextoInvalido,
                $"O texto do termo é obrigatório, tem entre {TextoMinLength} e {TextoMaxLength} caracteres depois de normalizado e não contém caractere de controle além da quebra de linha e da tabulação."));
        }
    }

    private static IEnumerable<FieldError> ErrosDoAto(string? ato)
    {
        if (ato is null)
        {
            yield return new("ato", new DomainError(
                TermoErrorCodes.AtoInvalido,
                $"O ato que aprova a versão é obrigatório e tem até {AtoMaxLength} caracteres, sem caractere de controle."));
        }
    }

    private static bool TemControleProibido(string texto) =>
        texto.Any(static c => char.IsControl(c) && c is not '\n' and not '\t');

    // A normalização Unicode não aceita metade de um par de surrogates: o texto que a traz é recusado, não corrigido.
    private static bool TemSurrogateSolto(string texto)
    {
        for (int i = 0; i < texto.Length; i++)
        {
            if (char.IsHighSurrogate(texto[i]) && i + 1 < texto.Length && char.IsLowSurrogate(texto[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(texto[i]))
            {
                return true;
            }
        }

        return false;
    }
}
