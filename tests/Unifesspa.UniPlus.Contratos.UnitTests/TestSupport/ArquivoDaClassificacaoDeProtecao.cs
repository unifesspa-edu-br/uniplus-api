namespace Unifesspa.UniPlus.Contratos.UnitTests.TestSupport;

using System.IO;

using Unifesspa.UniPlus.TestSupport;

/// <summary>
/// O arquivo versionado que guarda a classificação de proteção de cada coluna do modelo (ADR-0153): o
/// caminho dele e a leitura dele ficam só aqui, para que os testes que o usam nunca leiam arquivos diferentes.
/// </summary>
internal static class ArquivoDaClassificacaoDeProtecao
{
    private const string CaminhoDoArquivo = "tests/Unifesspa.UniPlus.Contratos.UnitTests/Conferencias/ClassificacaoDeProtecaoDoModelo.txt";

    internal static string Caminho() => Path.Join(LocalizadorDaSolucao.Locate(), CaminhoDoArquivo);

    internal static string LerTexto() => File.ReadAllText(Caminho()).ReplaceLineEndings("\n");

    internal static string[] LerLinhas() => File.ReadAllLines(Caminho());

    internal static void Regravar(string conteudo) => File.WriteAllText(Caminho(), conteudo);
}
