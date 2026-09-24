namespace Unifesspa.UniPlus.Kernel.Extensions;

using System.Buffers;
using System.Globalization;
using System.Text;

/// <summary>
/// Caracteres que não aparecem como texto: os de controle (categoria Cc, que inclui U+0000 e as
/// quebras de linha), os de formatação (Cf, que inclui os controles bidirecionais como U+202E) e
/// os separadores de linha e de parágrafo (U+2028 e U+2029). Num texto que volta em mensagem de
/// erro e em log, eles quebram a linha ou invertem a leitura do que vem depois.
/// </summary>
public static class CaracteresInvisiveis
{
    /// <summary>A categoria é de caractere invisível: controle, formatação ou separador de linha ou de parágrafo.</summary>
    public static bool EhInvisivel(UnicodeCategory categoria) =>
        categoria is UnicodeCategory.Control
            or UnicodeCategory.Format
            or UnicodeCategory.LineSeparator
            or UnicodeCategory.ParagraphSeparator;

    /// <summary>
    /// O texto tem algum caractere invisível, ou um surrogate sem par. Este último não é
    /// caractere nenhum, e a normalização Unicode recusaria o texto com exceção.
    /// </summary>
    public static bool Contem(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        ReadOnlySpan<char> resto = texto;
        while (!resto.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(resto, out Rune caractere, out int lidos) != OperationStatus.Done
                || EhInvisivel(Rune.GetUnicodeCategory(caractere)))
            {
                return true;
            }

            resto = resto[lidos..];
        }

        return false;
    }

    /// <summary>
    /// Um texto recebido, pronto para voltar numa mensagem: limitado a
    /// <paramref name="tamanhoMaximo"/> caracteres — sem partir um par substituto — e com cada
    /// caractere invisível trocado por "?", para não reescrever a linha em que cai.
    /// </summary>
    public static string ParaEco(string texto, int tamanhoMaximo)
    {
        ArgumentNullException.ThrowIfNull(texto);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tamanhoMaximo);

        string parte = texto;
        string reticencias = string.Empty;
        if (texto.Length > tamanhoMaximo)
        {
            int corte = char.IsHighSurrogate(texto[tamanhoMaximo - 1]) ? tamanhoMaximo - 1 : tamanhoMaximo;
            parte = texto[..corte];
            reticencias = "…";
        }

        return Substituir(parte, '?') + reticencias;
    }

    /// <summary>
    /// O texto com cada caractere invisível, e cada surrogate sem par, trocado por
    /// <paramref name="substituto"/> — para ecoar um texto recebido sem que ele reescreva a
    /// linha em que cai. A classificação é por caractere Unicode, não por unidade UTF-16: os
    /// caracteres de formatação fora do plano básico, como as tags U+E0000–U+E007F, também
    /// são trocados.
    /// </summary>
    public static string Substituir(string texto, char substituto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        StringBuilder resultado = new(texto.Length);
        ReadOnlySpan<char> resto = texto;
        while (!resto.IsEmpty)
        {
            OperationStatus situacao = Rune.DecodeFromUtf16(resto, out Rune caractere, out int lidos);
            int consumidos = Math.Max(lidos, 1);
            if (situacao != OperationStatus.Done || EhInvisivel(Rune.GetUnicodeCategory(caractere)))
            {
                resultado.Append(substituto);
            }
            else
            {
                resultado.Append(resto[..consumidos]);
            }

            resto = resto[consumidos..];
        }

        return resultado.ToString();
    }
}
