namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;

using System.Globalization;
using System.Security.Cryptography;

/// <summary>O código de confirmação: oito dígitos de um gerador criptográfico.</summary>
internal static class CodigoDeConfirmacao
{
    public const int Digitos = 8;

    public static string Gerar() =>
        RandomNumberGenerator.GetInt32(0, 100_000_000).ToString("D8", CultureInfo.InvariantCulture);

    /// <summary>Texto que só tem a forma de um código; o que não tem nem chega ao cálculo do resumo.</summary>
    public static bool TemAForma(string? texto) =>
        texto is { Length: Digitos } && texto.All(char.IsAsciiDigit);
}
