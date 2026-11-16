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
    private static readonly char[] QuebrasDeLinhaETabulacao = ['\n', '\r', '\t'];

    /// <summary>
    /// Quantas vezes um texto em NFC pode crescer ao ser decomposto (UAX #15): um caractere
    /// composto corresponde a no máximo quatro unidades na forma decomposta.
    /// </summary>
    private const int FatorMaximoDeDecomposicao = 4;

    /// <summary>
    /// O texto aparado e em NFC — a forma em que um código de referência é gravado e comparado. O texto
    /// que não se normaliza fica só aparado: não pode ser um código gravado, e a recusa é de quem o grava.
    /// </summary>
    public static string AparadoEmNfc(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);
        string aparado = texto.Trim();
        return TentarNormalizar(aparado, out string normalizado) ? normalizado : aparado;
    }

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
        Tentar(texto, maximo, static t => CaracteresInvisiveis.Contem(t), out normalizado);

    /// <summary>
    /// Como <see cref="TentarNfc"/>, para texto livre de várias linhas — descrição, justificativa —: aceita
    /// a quebra de linha (<c>\n</c> e <c>\r</c>) e a tabulação, e continua recusando o caractere nulo, os
    /// demais de controle e os de formatação, como os controles bidirecionais.
    /// </summary>
    public static SituacaoDoTexto TentarNfcDeTextoDeVariasLinhas(string texto, int maximo, out string normalizado) =>
        Tentar(texto, maximo, static t => CaracteresInvisiveis.Contem(t, QuebrasDeLinhaETabulacao), out normalizado);

    /// <summary>
    /// Como <see cref="TentarNfc"/>, para texto livre que não volta em mensagem nem em log: aceita
    /// caractere invisível — tabulação, quebra de linha, hífen condicional —, comum em texto
    /// jurídico colado de outro documento. Só o caractere nulo continua recusado: o Postgres não
    /// o grava.
    /// </summary>
    public static SituacaoDoTexto TentarNfcDeTextoLivre(string texto, int maximo, out string normalizado) =>
        Tentar(texto, maximo, static t => t.Contains((char)0), out normalizado);

    private static SituacaoDoTexto Tentar(string texto, int maximo, Func<string, bool> temCaractereRecusado, out string normalizado)
    {
        ArgumentNullException.ThrowIfNull(texto);

        normalizado = string.Empty;
        if (texto.Length > maximo * FatorMaximoDeDecomposicao)
        {
            return SituacaoDoTexto.TamanhoExcedido;
        }

        return temCaractereRecusado(texto) || !TentarNormalizar(texto, out string emNfc)
            ? SituacaoDoTexto.CaractereInvalido
            : CabeNoLimite(emNfc, maximo, out normalizado);
    }

    private static SituacaoDoTexto CabeNoLimite(string emNfc, int maximo, out string normalizado)
    {
        normalizado = emNfc.Length > maximo ? string.Empty : emNfc;
        return emNfc.Length > maximo ? SituacaoDoTexto.TamanhoExcedido : SituacaoDoTexto.Valido;
    }
}
