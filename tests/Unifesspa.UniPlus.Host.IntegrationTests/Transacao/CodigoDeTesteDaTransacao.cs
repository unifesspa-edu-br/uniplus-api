namespace Unifesspa.UniPlus.Host.IntegrationTests.Transacao;

using System.Globalization;

/// <summary>
/// Um código distinto a cada chamada, no formato que as duas entidades aceitam: só letras maiúsculas separadas por
/// sublinhado, que é o do tipo de ato (o do motivo também admite dígitos). Os dígitos hexadecimais do identificador viram
/// letras.
/// </summary>
internal static class CodigoDeTesteDaTransacao
{
    internal static string Novo(string prefixo) =>
        $"{prefixo}_{string.Concat(Guid.NewGuid().ToString("N").Select(static c => (char)('A' + int.Parse(c.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture))))}";
}
