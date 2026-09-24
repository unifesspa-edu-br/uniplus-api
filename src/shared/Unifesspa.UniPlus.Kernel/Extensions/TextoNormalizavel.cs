namespace Unifesspa.UniPlus.Kernel.Extensions;

using System.Text;

/// <summary>Como terminou a tentativa de levar um texto à forma NFC dentro de um limite.</summary>
public enum SituacaoDoTexto
{
    CaractereInvalido = 0,
    TamanhoExcedido = 1,
    Valido = 2,
}

/// <summary>
/// Normalização Unicode (NFC) que não lança: <see cref="string.Normalize()"/> recusa com
/// exceção os não-caracteres (como U+FFFE) e os surrogates sem par, e um texto recebido de fora
/// não pode virar erro 500.
/// </summary>
public static class TextoNormalizavel
{
    /// <summary>
    /// Quantas vezes um texto em NFC pode crescer ao ser decomposto (UAX #15): um caractere
    /// composto corresponde a no máximo quatro unidades na forma decomposta.
    /// </summary>
    private const int FatorMaximoDeDecomposicao = 4;

    /// <summary>O texto em NFC, ou <see langword="false"/> quando ele não pode ser normalizado.</summary>
    public static bool TentarNormalizar(string texto, out string normalizado)
    {
        ArgumentNullException.ThrowIfNull(texto);

        try
        {
            normalizado = texto.Normalize(NormalizationForm.FormC);
            return true;
        }
        catch (ArgumentException)
        {
            normalizado = string.Empty;
            return false;
        }
    }

    /// <summary>
    /// Leva o texto a NFC se ele couber em <paramref name="maximo"/> caracteres nessa forma e não
    /// tiver caractere invisível (<see cref="CaracteresInvisiveis"/>). Texto maior que o teto —
    /// o quanto um texto que cabe pode ocupar na forma mais decomposta — é recusado antes de ser
    /// varrido e normalizado.
    /// </summary>
    public static SituacaoDoTexto TentarNfc(string texto, int maximo, out string normalizado) =>
        Tentar(texto, maximo, recusarInvisiveis: true, out normalizado);

    /// <summary>
    /// Como <see cref="TentarNfc"/>, para texto livre que não volta em mensagem nem em log: aceita
    /// caractere invisível — tabulação, quebra de linha, hífen condicional —, comum em texto
    /// jurídico colado de outro documento. Só o caractere nulo continua recusado: o Postgres não
    /// o grava.
    /// </summary>
    public static SituacaoDoTexto TentarNfcDeTextoLivre(string texto, int maximo, out string normalizado) =>
        Tentar(texto, maximo, recusarInvisiveis: false, out normalizado);

    private static SituacaoDoTexto Tentar(string texto, int maximo, bool recusarInvisiveis, out string normalizado)
    {
        ArgumentNullException.ThrowIfNull(texto);

        normalizado = string.Empty;
        if (texto.Length > maximo * FatorMaximoDeDecomposicao)
        {
            return SituacaoDoTexto.TamanhoExcedido;
        }

        bool temInvisivelRecusado = recusarInvisiveis ? CaracteresInvisiveis.Contem(texto) : texto.Contains((char)0);
        if (temInvisivelRecusado || !TentarNormalizar(texto, out string emNfc))
        {
            return SituacaoDoTexto.CaractereInvalido;
        }

        if (emNfc.Length > maximo)
        {
            return SituacaoDoTexto.TamanhoExcedido;
        }

        normalizado = emNfc;
        return SituacaoDoTexto.Valido;
    }
}
