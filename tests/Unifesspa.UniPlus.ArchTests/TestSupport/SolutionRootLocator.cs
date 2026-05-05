namespace Unifesspa.UniPlus.ArchTests.TestSupport;

using System.IO;

/// <summary>
/// Helper compartilhado entre fitness functions solution-level que precisam
/// resolver caminhos relativos ao repositório (baselines OpenAPI, projetos
/// de domínio, contracts/). Sobe a partir de <see cref="AppContext.BaseDirectory"/>
/// procurando <c>UniPlus.slnx</c> — resiliente a mudanças de target framework
/// e path do bin de teste.
/// </summary>
internal static class SolutionRootLocator
{
    internal static string Locate()
    {
        string current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "UniPlus.slnx")))
                return current;
            current = Directory.GetParent(current)?.FullName ?? string.Empty;
        }

        throw new DirectoryNotFoundException(
            "UniPlus.slnx não encontrado a partir de AppContext.BaseDirectory; estrutura do repositório alterada?");
    }
}
