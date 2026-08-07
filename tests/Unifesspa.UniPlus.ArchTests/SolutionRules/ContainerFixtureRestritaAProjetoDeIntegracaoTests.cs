namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.IO;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using TestSupport;

/// <summary>
/// Fitness function que fecha a porta contrária ao filtro estrutural de
/// <c>dotnet test</c>: <c>FullyQualifiedName!~IntegrationTests</c> só exclui
/// testes com Docker se nenhuma classe fora de um projeto <c>*.IntegrationTests</c>
/// subir um container Testcontainers (ver AGENTS.md e CONTRIBUTING.md).
/// </summary>
/// <remarks>
/// A varredura automatiza, como fitness function permanente, a mesma verificação
/// textual que fundamentou a escolha do filtro estrutural: nenhum arquivo fora de
/// <c>tests/*.IntegrationTests</c> referencia Testcontainers. As duas exceções
/// (<see cref="ProjetosDeFixtureExcluidos"/>) são bibliotecas de fixture puras —
/// <c>IsTestProject=false</c>, sem <c>[Fact]</c> próprio — consumidas apenas pelos
/// projetos <c>*.IntegrationTests</c>; não produzem caso de teste executável fora
/// do escopo Docker, então referenciar Testcontainers ali não quebra a invariante
/// que o filtro estrutural depende.
/// </remarks>
public sealed class ContainerFixtureRestritaAProjetoDeIntegracaoTests
{
    private static readonly Regex[] PadroesDeUsoDeTestcontainers =
    [
        new(@"^\s*using\s+Testcontainers", RegexOptions.Multiline | RegexOptions.Compiled),
        new(@"new\s+PostgreSqlBuilder\s*\(", RegexOptions.Compiled),
        new(@"new\s+ContainerBuilder\s*\(", RegexOptions.Compiled),
    ];

    private static readonly string[] ProjetosDeFixtureExcluidos =
    [
        "Unifesspa.UniPlus.Monolito.TestSupport",
        "Unifesspa.UniPlus.IntegrationTests.Fixtures",
    ];

    [Fact(DisplayName = "Nenhuma classe fora de *.IntegrationTests referencia Testcontainers diretamente")]
    public void NenhumArquivoForaDeIntegrationTestsUsaTestcontainers()
    {
        string solutionRoot = SolutionRootLocator.Locate();
        string testsRoot = Path.Combine(solutionRoot, "tests");

        List<string> violacoes = [];

        foreach (string arquivo in Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (EstaEmBinOuObj(arquivo))
            {
                continue;
            }

            string relativo = Path.GetRelativePath(testsRoot, arquivo);
            string nomeProjeto = relativo.Split(Path.DirectorySeparatorChar)[0];

            // O nome do projeto já codifica a necessidade de Docker — é a
            // mesma estrutura que o filtro `FullyQualifiedName~IntegrationTests`
            // usa para incluir a suíte de integração inteira.
            if (nomeProjeto.EndsWith(".IntegrationTests", StringComparison.Ordinal)
                || ProjetosDeFixtureExcluidos.Contains(nomeProjeto, StringComparer.Ordinal))
            {
                continue;
            }

            string conteudo = File.ReadAllText(arquivo);

            foreach (Regex padrao in PadroesDeUsoDeTestcontainers)
            {
                if (padrao.IsMatch(conteudo))
                {
                    violacoes.Add($"{relativo}: casa com '{padrao}'.");
                    break;
                }
            }
        }

        violacoes.Should().BeEmpty(
            "o filtro estrutural (FullyQualifiedName!~IntegrationTests / FullyQualifiedName~IntegrationTests) só "
            + "protege a suíte sem Docker enquanto nenhuma classe fora de tests/*.IntegrationTests subir um "
            + "container Testcontainers — mova a classe (ou a fixture) para um projeto *.IntegrationTests. "
            + "Violações:\n" + string.Join("\n", violacoes));
    }

    private static bool EstaEmBinOuObj(string caminho) =>
        caminho.Split(Path.DirectorySeparatorChar).Any(segmento =>
            string.Equals(segmento, "bin", StringComparison.Ordinal)
            || string.Equals(segmento, "obj", StringComparison.Ordinal));
}
