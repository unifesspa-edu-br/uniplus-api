namespace Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using System.Security.Cryptography;
using System.Text.RegularExpressions;

/// <summary>
/// O SHA-256 de um conteúdo, em hexadecimal minúsculo de 64 caracteres: o que o módulo grava para reconhecer que um
/// conteúdo é o mesmo sem guardá-lo. Quem decide o que entra no conteúdo, e em que ordem, é quem o calcula; o tipo só
/// garante a forma do resultado.
/// </summary>
public sealed partial record ResumoSha256
{
    /// <summary>Quantidade de caracteres hexadecimais de um SHA-256.</summary>
    public const int Comprimento = 64;

    public string Valor { get; }

    private ResumoSha256(string valor) => Valor = valor;

    /// <summary>
    /// O resumo de um texto já calculado: 64 caracteres hexadecimais em minúsculas, nada além. Quem chama lê o texto de uma
    /// fonte confiável, como o banco, e o texto fora da forma falha alto em vez de virar valor nulo ou silenciosamente aceito.
    /// </summary>
    /// <exception cref="ArgumentException">O texto não tem a forma de um SHA-256 em hexadecimal minúsculo.</exception>
    public static ResumoSha256 De(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        return Formato().IsMatch(texto)
            ? new ResumoSha256(texto)
            : throw new ArgumentException($"O resumo deve ter {Comprimento} caracteres hexadecimais minúsculos.", nameof(texto));
    }

    /// <summary>Calcula o resumo do conteúdo informado.</summary>
    public static ResumoSha256 DoConteudo(ReadOnlySpan<byte> conteudo) =>
        new(Convert.ToHexStringLower(SHA256.HashData(conteudo)));

    public override string ToString() => Valor;

    [GeneratedRegex(@"^[0-9a-f]{64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Formato();
}
