namespace Unifesspa.UniPlus.ArchTests.TestSupport;

using System.IO;

/// <summary>
/// O arquivo versionado que guarda a classificação de proteção de cada coluna do modelo (ADR-0153): o
/// caminho dele e a leitura dele ficam só aqui, para que os testes que o usam nunca leiam arquivos diferentes.
/// </summary>
internal static class RetratoDaClassificacaoDeProtecao
{
    private const string ArquivoDoRetrato = "tests/Unifesspa.UniPlus.ArchTests/SolutionRules/ClassificacaoDeProtecaoDoModelo.retrato.txt";

    internal static string Caminho() => Path.Join(SolutionRootLocator.Locate(), ArquivoDoRetrato);

    internal static string LerTexto() => File.ReadAllText(Caminho()).ReplaceLineEndings("\n");

    internal static string[] LerLinhas() => File.ReadAllLines(Caminho());

    internal static void Regravar(string conteudo) => File.WriteAllText(Caminho(), conteudo);
}
