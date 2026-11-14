namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;

using System.Globalization;

using Unifesspa.UniPlus.Publicacoes.Domain.Entities;

/// <summary>
/// Como a recusa nomeia o ato que a causa: pela numeração oficial (série, número e ano), que o operador reconhece, e nunca
/// pelo identificador.
/// </summary>
internal static class NumeracaoDoAto
{
    public static string Descrever(AtoNormativo ato)
    {
        ArgumentNullException.ThrowIfNull(ato);

        return ato.Numero is null
            ? string.Create(CultureInfo.InvariantCulture, $"{ato.Serie} sem número de {ato.Ano}")
            : string.Create(CultureInfo.InvariantCulture, $"{ato.Serie} nº {ato.Numero}/{ato.Ano}");
    }
}
