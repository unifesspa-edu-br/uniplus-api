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
/// <para>
/// A varredura automatiza, como fitness function permanente, a mesma verificação
/// textual que fundamentou a escolha do filtro estrutural: nenhum arquivo fora de
/// <c>tests/*.IntegrationTests</c> referencia Testcontainers. As duas exceções
/// (<see cref="ProjetosDeFixtureExcluidos"/>) são bibliotecas de fixture puras —
/// <c>IsTestProject=false</c>, sem <c>[Fact]</c> próprio — consumidas apenas pelos
/// projetos <c>*.IntegrationTests</c>; não produzem caso de teste executável fora
/// do escopo Docker, então referenciar Testcontainers ali não quebra a invariante
/// que o filtro estrutural depende.
/// </para>
/// <para>
/// A varredura acima decide por diretório do projeto, mas o filtro do
/// <c>dotnet test</c> decide por <c>FullyQualifiedName</c> (namespace + classe +
/// método) — os dois só coincidem enquanto o namespace de todo teste dentro de
/// um projeto <c>*.IntegrationTests</c> contiver <c>IntegrationTests</c>.
/// <see cref="TestesEmProjetoDeIntegracaoDeclaramNamespaceComIntegrationTests"/>
/// fecha essa lacuna.
/// </para>
/// <para>
/// A exceção das bibliotecas de fixture, por sua vez, só é segura enquanto
/// nada fora de <c>*.IntegrationTests</c> (ou das próprias bibliotecas) as
/// referenciar — um projeto <c>.UnitTests</c>/<c>.ArchTests</c> que passasse a
/// depender de <see cref="ProjetosDeFixtureExcluidos"/> herdaria uma fixture de
/// container sem que o próprio texto "Testcontainers" aparecesse nele, furando
/// a varredura acima em silêncio.
/// <see cref="SomenteConsumidoresLegitimosReferenciamBibliotecasDeFixture"/>
/// fecha essa lacuna.
/// </para>
/// </remarks>
public sealed class ContainerFixtureRestritaAProjetoDeIntegracaoTests
{
    private static readonly Regex[] PadroesDeUsoDeTestcontainers =
    [
        new(@"^\s*using\s+Testcontainers", RegexOptions.Multiline | RegexOptions.Compiled),
        new(@"new\s+PostgreSqlBuilder\s*\(", RegexOptions.Compiled),
        new(@"new\s+ContainerBuilder\s*\(", RegexOptions.Compiled),
    ];

    private static readonly Regex PadraoDeDeclaracaoDeTeste =
        new(@"\[\s*(Fact|Theory)\b", RegexOptions.Compiled);

    private static readonly Regex PadraoDeNamespace =
        new(@"^\s*namespace\s+([A-Za-z0-9_.]+)\s*[;{]", RegexOptions.Multiline | RegexOptions.Compiled);

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

    [Fact(DisplayName = "Todo teste dentro de *.IntegrationTests declara namespace que contém IntegrationTests")]
    public void TestesEmProjetoDeIntegracaoDeclaramNamespaceComIntegrationTests()
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

            if (!nomeProjeto.EndsWith(".IntegrationTests", StringComparison.Ordinal))
            {
                continue;
            }

            string conteudo = File.ReadAllText(arquivo);

            // Só arquivos que efetivamente declaram um caso de teste ([Fact]/[Theory])
            // têm FullyQualifiedName avaliado pelo filtro — classes de apoio (fixtures,
            // collections, stubs) não produzem caso de teste executável.
            if (!PadraoDeDeclaracaoDeTeste.IsMatch(conteudo))
            {
                continue;
            }

            Match match = PadraoDeNamespace.Match(conteudo);
            string? namespaceDeclarado = match.Success ? match.Groups[1].Value : null;

            if (namespaceDeclarado is null
                || !namespaceDeclarado.Contains("IntegrationTests", StringComparison.Ordinal))
            {
                violacoes.Add(
                    $"{relativo}: declara [Fact]/[Theory] em namespace "
                    + $"'{namespaceDeclarado ?? "(ausente)"}', que não contém 'IntegrationTests'.");
            }
        }

        violacoes.Should().BeEmpty(
            "o filtro estrutural (FullyQualifiedName!~IntegrationTests / FullyQualifiedName~IntegrationTests) "
            + "opera sobre o FullyQualifiedName do teste — namespace + classe + método —, não sobre o diretório "
            + "do projeto; um teste fisicamente dentro de *.IntegrationTests cujo namespace não contenha "
            + "'IntegrationTests' entra na suíte sem Docker mesmo estando no projeto certo. Ajuste o namespace "
            + "para refletir o projeto. Violações:\n" + string.Join("\n", violacoes));
    }

    [Fact(DisplayName = "Só *.IntegrationTests e as bibliotecas de fixture referenciam Monolito.TestSupport ou IntegrationTests.Fixtures")]
    public void SomenteConsumidoresLegitimosReferenciamBibliotecasDeFixture()
    {
        string solutionRoot = SolutionRootLocator.Locate();
        string testsRoot = Path.Combine(solutionRoot, "tests");

        List<string> violacoes = [];

        foreach (string csproj in Directory.EnumerateFiles(testsRoot, "*.csproj", SearchOption.AllDirectories))
        {
            if (EstaEmBinOuObj(csproj))
            {
                continue;
            }

            string relativo = Path.GetRelativePath(testsRoot, csproj);
            string nomeProjeto = relativo.Split(Path.DirectorySeparatorChar)[0];

            bool ehConsumidorLegitimo = nomeProjeto.EndsWith(".IntegrationTests", StringComparison.Ordinal)
                || ProjetosDeFixtureExcluidos.Contains(nomeProjeto, StringComparer.Ordinal);

            if (ehConsumidorLegitimo)
            {
                continue;
            }

            string conteudo = File.ReadAllText(csproj);

            foreach (string biblioteca in ProjetosDeFixtureExcluidos)
            {
                if (conteudo.Contains($"{biblioteca}.csproj", StringComparison.Ordinal))
                {
                    violacoes.Add($"{relativo}: referencia {biblioteca}, que não é *.IntegrationTests nem biblioteca de fixture.");
                }
            }
        }

        violacoes.Should().BeEmpty(
            "Monolito.TestSupport e IntegrationTests.Fixtures são excluídos incondicionalmente da varredura de "
            + "Testcontainers acima por serem bibliotecas de fixture puras consumidas só por *.IntegrationTests; "
            + "se um projeto fora desse conjunto passar a referenciá-las, herda uma fixture de container sem que "
            + "o próprio projeto contenha o texto 'Testcontainers', furando o filtro estrutural "
            + "(FullyQualifiedName!~IntegrationTests) sem que a outra varredura desta classe perceba. "
            + "Violações:\n" + string.Join("\n", violacoes));
    }

    private static bool EstaEmBinOuObj(string caminho) =>
        caminho.Split(Path.DirectorySeparatorChar).Any(segmento =>
            string.Equals(segmento, "bin", StringComparison.Ordinal)
            || string.Equals(segmento, "obj", StringComparison.Ordinal));
}
