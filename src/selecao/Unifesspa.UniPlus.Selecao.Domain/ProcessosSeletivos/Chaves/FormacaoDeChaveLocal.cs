namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Chaves;

using System.Globalization;
using System.Text;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class FormacaoDeChaveLocal
{
    internal static ChaveLocal Formar(string prefixo, string texto, ChavesEmUso chavesEmUso)
    {
        string baseDaChave = BaseDaChave(prefixo, Normalizar(texto));
        int numero = 1;
        while (true)
        {
            string sufixo = numero == 1 ? string.Empty : $"_{numero.ToString(CultureInfo.InvariantCulture)}";
            string valor = Limitar(baseDaChave, ChaveLocal.ComprimentoMaximo - sufixo.Length) + sufixo;
            ChaveLocal chave = ChaveLocal.Criar(valor).Value;
            if (!chavesEmUso.Contem(chave))
            {
                return chave;
            }

            numero++;
        }
    }

    private static string Normalizar(string texto)
    {
        var resultado = new StringBuilder();
        foreach (char caractere in texto.Normalize(NormalizationForm.FormD).ToLowerInvariant())
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caractere) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            resultado.Append(char.IsAsciiLetterOrDigit(caractere) ? caractere : '_');
        }

        return string.Join('_', resultado.ToString().Split('_', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string BaseDaChave(string prefixo, string texto) =>
        texto.Length == 0 ? prefixo
        : texto == prefixo || texto.StartsWith(prefixo + "_", StringComparison.Ordinal) ? texto
        : prefixo + "_" + texto;

    private static string Limitar(string valor, int limite)
    {
        if (valor.Length <= limite)
        {
            return valor;
        }

        int separador = valor.LastIndexOf('_', limite, limite + 1);
        return separador > 0 ? valor[..separador] : valor[..limite];
    }
}
