namespace Unifesspa.UniPlus.TestSupport;

/// <summary>Guarda um resultado para revisão sem alterar a referência aprovada.</summary>
public static class ResultadoDeConferencia
{
    public static string Mensagem(string resultado, string arquivoVersionado)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        ArgumentNullException.ThrowIfNull(arquivoVersionado);
        return $"resultado obtido: {resultado}\npara aprovar a partir da raiz do repositório: cp \"{Escapar(resultado)}\" \"{Escapar(arquivoVersionado)}\"";
    }

    private static string Escapar(string caminho) => caminho.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("$", "\\$", StringComparison.Ordinal)
        .Replace("`", "\\`", StringComparison.Ordinal);

    public static string Gravar(string nome, byte[] conteudo)
    {
        string pasta = Path.Join(Path.GetTempPath(), "uniplus-resultados", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        string caminho = Path.Join(pasta, Path.GetFileName(nome));
        File.WriteAllBytes(caminho, conteudo);
        return caminho;
    }

    public static string Gravar(string nome, string conteudo) => Gravar(nome, System.Text.Encoding.UTF8.GetBytes(conteudo));
}
