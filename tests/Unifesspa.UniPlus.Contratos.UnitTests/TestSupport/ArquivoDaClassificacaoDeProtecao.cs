namespace Unifesspa.UniPlus.Contratos.UnitTests.TestSupport;

using System.IO;


/// <summary>
/// O arquivo versionado que guarda a classificação de proteção de cada coluna do modelo (ADR-0153): o
/// caminho dele e a leitura dele ficam só aqui, para que os testes que o usam nunca leiam arquivos diferentes.
/// </summary>
internal static class ArquivoDaClassificacaoDeProtecao
{
    private const string CaminhoDoArquivo = "Conferencias/ClassificacaoDeProtecaoDoModelo.txt";

    internal static string Caminho() => Path.Join(AppContext.BaseDirectory, CaminhoDoArquivo);

    internal static string LerTexto() => File.ReadAllText(Caminho()).ReplaceLineEndings("\n");

    internal static string[] LerLinhas() => File.ReadAllLines(Caminho());
}
