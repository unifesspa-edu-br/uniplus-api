namespace Unifesspa.UniPlus.Testes.Compartilhado;

using System.Globalization;

/// <summary>Os três envelopes congelados de referência, com os marcadores de identificador trocados por identificadores reais.</summary>
internal static class EnvelopesDeReferenciaDeTeste
{
    internal static string Ler(string arquivo)
    {
        string caminho = Path.Join(AppContext.BaseDirectory, "ProcessosSeletivos", "Fixtures", arquivo);
        string[] trechos = File.ReadAllText(caminho).Split("<<id-");
        return trechos[0] + string.Concat(trechos.Skip(1).Select(IdentificadorSeguidoDoRestoDoTrecho));
    }

    private static string IdentificadorSeguidoDoRestoDoTrecho(string trecho)
    {
        int fim = trecho.IndexOf(">>", StringComparison.Ordinal);
        Guid identificador = new(int.Parse(trecho[..fim], CultureInfo.InvariantCulture), 0, 0, new byte[8]);
        return identificador + trecho[(fim + 2)..];
    }
}
