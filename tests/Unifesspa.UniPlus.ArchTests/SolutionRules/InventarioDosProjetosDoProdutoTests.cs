namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.IO;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.TestSupport;

/// <summary>
/// O inventário dos assemblies do produto (<see cref="AssembliesDoProduto"/>) cobre todo projeto de <c>src</c>, e os
/// executáveis do produto são exatamente o Host, o Portal e o Verificador. A conferência dos códigos dos enums na subida
/// roda em cada executável (ADR-0176) e alcança os demais projetos pelas referências deles: um executável a mais, ou um
/// projeto fora do inventário, ficaria sem a conferência ou sem as regras de arquitetura que o inventário alimenta.
/// </summary>
public sealed class InventarioDosProjetosDoProdutoTests
{
    [Fact(DisplayName = "Todo projeto de src tem o assembly no inventário dos assemblies do produto")]
    public void TodoProjetoDeSrc_TemOAssemblyNoInventario()
    {
        string[] projetos = NomesDosProjetosDeSrc();
        HashSet<string?> doInventario = [.. AssembliesDoProduto.Todos.Select(static a => a.GetName().Name)];

        projetos.Should().NotBeEmpty("sem projeto descoberto, a conferência passaria vazia");
        projetos.Where(projeto => !doInventario.Contains(projeto)).Order(StringComparer.Ordinal).Should().BeEmpty(
            "o projeto fora de AssembliesDoProduto fica sem as regras de arquitetura do produto: adicione a referência de projeto ao ArchTests e "
            + "o tipo-âncora em AssembliesDoProduto");
    }

    [Fact(DisplayName = "Os assemblies do produto com ponto de entrada são exatamente o Host, o Portal e o Verificador")]
    public void AssembliesDoProduto_ComPontoDeEntrada_SaoOsTresExecutaveis()
    {
        string?[] comPontoDeEntrada = [.. AssembliesDoProduto.Todos.Where(static a => a.EntryPoint is not null).Select(static a => a.GetName().Name)];
        string?[] executaveis = [.. ExecutaveisDoProduto.Todos.Select(static a => a.GetName().Name)];

        comPontoDeEntrada.Should().BeEquivalentTo(
            executaveis,
            "o executável a mais precisa da conferência dos enums na subida, e o que deixou de ser executável não a precisa mais");
    }

    // O nome do assembly de um projeto é o do arquivo de projeto.
    private static string[] NomesDosProjetosDeSrc() =>
        [.. Directory.EnumerateFiles(Path.Join(SolutionRootLocator.Locate(), "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(static caminho => Path.GetFileNameWithoutExtension(caminho.AsSpan()).ToString())];
}
