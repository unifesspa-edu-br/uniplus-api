namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using System.Text;

/// <summary>
/// A regra do nome com que o administrador envia um arquivo ao processo — o nome que a pessoa
/// recebe no download. Única para o modelo de documento e para o documento a publicar.
/// </summary>
public static class NomeDeArquivoEnviado
{
    /// <summary>
    /// Tamanho máximo do nome, com a extensão. Cabe no <c>Content-Disposition</c> do download mesmo
    /// com os caracteres acentuados codificados.
    /// </summary>
    public const int TamanhoMaximo = 120;

    /// <summary>Os caracteres que o nome não pode ter, além dos de controle.</summary>
    public const string CaracteresProibidos = "/\\<>:\"|?*;";

    /// <summary>
    /// O nome aparado e normalizado em NFC — a forma que o envelope canônico emite —, terminado na
    /// extensão dada; nulo quando vazio, longo ou com caractere proibido. A extensão vem sem o ponto
    /// e pode ser vazia, quando o formato do arquivo não foi reconhecido: o nome ainda é conferido,
    /// para a recusa dele acumular com a do formato.
    /// </summary>
    public static string? Normalizar(string? nomeArquivo, string extensao)
    {
        ArgumentNullException.ThrowIfNull(extensao);

        string sufixo = extensao.Length == 0 ? string.Empty : "." + extensao;
        string nome = (nomeArquivo ?? string.Empty).Trim().Normalize(NormalizationForm.FormC);
        if (sufixo.Length > 0 && nome.EndsWith(sufixo, StringComparison.OrdinalIgnoreCase))
        {
            nome = nome[..^sufixo.Length].TrimEnd();
        }

        if (nome.Length == 0 || nome.Any(static c => char.IsControl(c) || CaracteresProibidos.Contains(c, StringComparison.Ordinal)))
        {
            return null;
        }

        string comExtensao = nome + sufixo;
        return comExtensao.Length <= TamanhoMaximo ? comExtensao : null;
    }
}
