namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using AwesomeAssertions;

using TestSupport;

/// <summary>
/// Fitness function que fecha a porta contrária ao filtro estrutural de
/// <c>dotnet test</c>: <c>FullyQualifiedName!~IntegrationTests</c> só exclui
/// testes com Docker se nenhum projeto fora de um projeto <c>*.IntegrationTests</c>
/// conseguir subir um container Testcontainers (ver AGENTS.md e CONTRIBUTING.md).
/// </summary>
/// <remarks>
/// <para>
/// <b>A detecção é estrutural, não textual.</b> Um projeto só consegue subir container
/// se referenciar o pacote Testcontainers — isso está no <c>.csproj</c> (<c>PackageReference</c>),
/// não no texto do código. Uma varredura por regex sobre C# persegue variações de escrita sem fim
/// (alias de <c>using</c>, qualificação de namespace, atributo de teste custom) — cada evasão
/// encontrada só motiva a próxima. A referência de pacote não tem essa saída: nenhum alias
/// contorna o fato de o projeto declarar (direta ou transitivamente, via <c>ProjectReference</c>
/// a outro projeto que declara) um <c>PackageReference</c> para <c>Testcontainers*</c>.
/// <see cref="SoProjetoDeIntegracaoReferenciaPacoteTestcontainers"/> constrói esse grafo a partir
/// dos <c>.csproj</c> sob <c>tests/</c> e calcula o fecho transitivo. As duas exceções
/// (<see cref="ProjetosDeFixtureExcluidos"/>) são bibliotecas de fixture puras —
/// <c>IsTestProject=false</c>, sem <c>[Fact]</c> próprio — consumidas apenas pelos
/// projetos <c>*.IntegrationTests</c>; não produzem caso de teste executável fora
/// do escopo Docker, então referenciar Testcontainers ali não quebra a invariante
/// que o filtro estrutural depende.
/// </para>
/// <para>
/// A varredura acima decide por projeto, mas o filtro do <c>dotnet test</c> decide por
/// <c>FullyQualifiedName</c> (namespace + classe + método) — os dois só coincidem enquanto
/// o namespace de todo arquivo dentro de um projeto <c>*.IntegrationTests</c> contiver
/// <c>IntegrationTests</c>. <see cref="ProjetoDeIntegracaoDeclaraNamespaceCoerenteComOFiltro"/>
/// fecha essa lacuna verificando **todo** arquivo <c>.cs</c> que declara namespace — não só os
/// que uma regex reconhece como contendo um caso de teste. Restringir a verificação a arquivos
/// com <c>[Fact]</c>/<c>[Theory]</c> tem o mesmo problema da varredura textual de Testcontainers:
/// qualquer forma de declarar teste que a regex não previu (atributo totalmente qualificado,
/// alias, framework de teste diferente) passa batido. Um arquivo de apoio (fixture, stub,
/// collection) com namespace divergente já é risco em potencial — não precisa conter um teste
/// para o <c>FullyQualifiedName</c> de uma classe vizinha no mesmo namespace ficar incoerente
/// com o diretório físico.
/// </para>
/// <para>
/// A exceção das bibliotecas de fixture, por sua vez, só é segura enquanto
/// nada fora de <c>*.IntegrationTests</c> (ou das próprias bibliotecas) as
/// referenciar — um projeto <c>.UnitTests</c>/<c>.ArchTests</c> que passasse a
/// depender de <see cref="ProjetosDeFixtureExcluidos"/> herdaria uma fixture de
/// container mesmo sem declarar <c>PackageReference</c> Testcontainers ele mesmo.
/// <see cref="SoProjetoDeIntegracaoReferenciaPacoteTestcontainers"/> já cobre esse caso —
/// o fecho transitivo por <c>ProjectReference</c> alcança o pacote através da biblioteca de
/// fixture, qualquer que seja a profundidade da cadeia. <see cref="SomenteConsumidoresLegitimosReferenciamBibliotecasDeFixture"/>
/// complementa com uma invariante mais estrita e independente de Testcontainers: as duas
/// bibliotecas de fixture são de uso exclusivo de <c>*.IntegrationTests</c> por contenção
/// arquitetural, não só porque hoje referenciam Testcontainers.
/// </para>
/// </remarks>
/// <remarks>
/// <para>
/// <b>Os nomes dos métodos evitam de propósito o token que o filtro usa.</b> O predicado
/// <c>FullyQualifiedName~IntegrationTests</c> casa por conteúdo, e o nome do método entra no
/// <c>FullyQualifiedName</c>; um método chamado <c>...IntegrationTests...</c> seria classificado
/// como teste de integração, sairia da suíte rápida e passaria a exigir Docker — justamente o
/// guarda que existe para proteger essa separação ficaria fora dela.
/// </para>
/// </remarks>
public sealed class ContainerFixtureRestritaAProjetoDeIntegracaoTests
{
    private static readonly Regex PadraoDeNamespace =
        new(@"^\s*namespace\s+([A-Za-z0-9_.]+)\s*[;{]", RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly string[] ProjetosDeFixtureExcluidos =
    [
        "Unifesspa.UniPlus.Monolito.TestSupport",
        "Unifesspa.UniPlus.IntegrationTests.Fixtures",
    ];

    [Fact(DisplayName = "Nenhum projeto fora de *.IntegrationTests alcança um pacote Testcontainers*, direto ou via outro projeto")]
    public void SoProjetoDeIntegracaoReferenciaPacoteTestcontainers()
    {
        string solutionRoot = SolutionRootLocator.Locate();
        string testsRoot = Path.Combine(solutionRoot, "tests");

        Dictionary<string, ProjetoDeTeste> projetos = CarregarProjetosDeTeste(testsRoot);
        Dictionary<string, bool> memo = [];
        List<string> violacoes = [];

        foreach (ProjetoDeTeste projeto in projetos.Values)
        {
            // O nome do projeto já codifica a necessidade de Docker — é a
            // mesma estrutura que o filtro `FullyQualifiedName~IntegrationTests`
            // usa para incluir a suíte de integração inteira.
            if (projeto.Nome.EndsWith(".IntegrationTests", StringComparison.Ordinal)
                || ProjetosDeFixtureExcluidos.Contains(projeto.Nome, StringComparer.Ordinal))
            {
                continue;
            }

            if (!AlcancaTestcontainers(projeto.Nome, projetos, memo, []))
            {
                continue;
            }

            string? pacoteDireto = projeto.PacotesReferenciados
                .FirstOrDefault(pacote => pacote.StartsWith("Testcontainers", StringComparison.Ordinal));

            violacoes.Add(pacoteDireto is not null
                ? $"{projeto.Nome}: referencia o pacote '{pacoteDireto}' diretamente."
                : $"{projeto.Nome}: alcança um pacote Testcontainers* por ProjectReference transitivo.");
        }

        violacoes.Should().BeEmpty(
            "o filtro estrutural (FullyQualifiedName!~IntegrationTests / FullyQualifiedName~IntegrationTests) só "
            + "protege a suíte sem Docker enquanto nenhum projeto fora de tests/*.IntegrationTests conseguir "
            + "subir um container — e um projeto só consegue subir container se referenciar, direta ou "
            + "transitivamente via ProjectReference, um pacote Testcontainers*. Mova o projeto (ou a referência) "
            + "para dentro de *.IntegrationTests. Violações:\n" + string.Join("\n", violacoes));
    }

    [Fact(DisplayName = "Todo arquivo com namespace dentro de *.IntegrationTests declara namespace que contém IntegrationTests")]
    public void ProjetoDeIntegracaoDeclaraNamespaceCoerenteComOFiltro()
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
            Match match = PadraoDeNamespace.Match(conteudo);

            // Arquivo sem declaração de namespace (ex.: um GlobalUsings.cs só com
            // `global using`) não produz FullyQualifiedName — nada para o filtro avaliar.
            if (!match.Success)
            {
                continue;
            }

            string namespaceDeclarado = match.Groups[1].Value;

            if (!namespaceDeclarado.Contains("IntegrationTests", StringComparison.Ordinal))
            {
                violacoes.Add($"{relativo}: declara namespace '{namespaceDeclarado}', que não contém 'IntegrationTests'.");
            }
        }

        violacoes.Should().BeEmpty(
            "o filtro estrutural (FullyQualifiedName!~IntegrationTests / FullyQualifiedName~IntegrationTests) "
            + "opera sobre o FullyQualifiedName do teste — namespace + classe + método —, não sobre o diretório "
            + "do projeto; um arquivo fisicamente dentro de *.IntegrationTests cujo namespace não contenha "
            + "'IntegrationTests' arrisca produzir um FullyQualifiedName que entra na suíte sem Docker mesmo "
            + "estando no projeto certo. Ajuste o namespace para refletir o projeto. Violações:\n"
            + string.Join("\n", violacoes));
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
            "Monolito.TestSupport e IntegrationTests.Fixtures são de uso exclusivo de *.IntegrationTests por "
            + "contenção arquitetural — não só porque hoje referenciam Testcontainers. Se um projeto fora desse "
            + "conjunto passar a referenciá-las, herda superfície de fixture (ApiFactoryBase, TestAuthHandler, "
            + "KeycloakContainerFixture) fora do escopo pretendido, mesmo quando isso não dispara a varredura de "
            + "Testcontainers em separado. Violações:\n" + string.Join("\n", violacoes));
    }

    private static bool EstaEmBinOuObj(string caminho) =>
        caminho.Split(Path.DirectorySeparatorChar).Any(segmento =>
            string.Equals(segmento, "bin", StringComparison.Ordinal)
            || string.Equals(segmento, "obj", StringComparison.Ordinal));

    private static Dictionary<string, ProjetoDeTeste> CarregarProjetosDeTeste(string testsRoot)
    {
        Dictionary<string, ProjetoDeTeste> projetos = [];

        foreach (string csproj in Directory.EnumerateFiles(testsRoot, "*.csproj", SearchOption.AllDirectories))
        {
            if (EstaEmBinOuObj(csproj))
            {
                continue;
            }

            ProjetoDeTeste projeto = CarregarProjeto(csproj);
            projetos[projeto.Nome] = projeto;
        }

        return projetos;
    }

    private static ProjetoDeTeste CarregarProjeto(string caminhoCsproj)
    {
        string nomeProjeto = Path.GetFileNameWithoutExtension(caminhoCsproj);
        string diretorioDoProjeto = Path.GetDirectoryName(caminhoCsproj)!;
        XDocument documento = XDocument.Load(caminhoCsproj);

        List<string> pacotes = [];
        foreach (XElement elemento in documento.Descendants("PackageReference"))
        {
            string? include = elemento.Attribute("Include")?.Value;
            if (!string.IsNullOrEmpty(include))
            {
                pacotes.Add(include);
            }
        }

        List<string> projetosReferenciados = [];
        foreach (XElement elemento in documento.Descendants("ProjectReference"))
        {
            string? include = elemento.Attribute("Include")?.Value;
            if (string.IsNullOrEmpty(include))
            {
                continue;
            }

            // O atributo Include mistura '/' e '\' entre .csproj (e às vezes dentro do
            // mesmo arquivo) — MSBuild normaliza os dois em qualquer plataforma, mas
            // System.IO.Path não: no Linux, '\' é só um caractere de nome de arquivo.
            string caminhoNormalizado = include.Replace('\\', '/');
            string caminhoAbsoluto = Path.GetFullPath(Path.Combine(diretorioDoProjeto, caminhoNormalizado));

            projetosReferenciados.Add(Path.GetFileNameWithoutExtension(caminhoAbsoluto));
        }

        return new ProjetoDeTeste(nomeProjeto, pacotes, projetosReferenciados);
    }

    /// <summary>
    /// Calcula se <paramref name="nomeProjeto"/> alcança um pacote <c>Testcontainers*</c>,
    /// direto ou por qualquer profundidade de <c>ProjectReference</c> dentro de <c>tests/</c>.
    /// Um projeto de produção (<c>src/</c>) referenciado nunca aparece em <paramref name="projetos"/>
    /// — a busca simplesmente não avança por ali, porque produção não referencia Testcontainers.
    /// </summary>
    private static bool AlcancaTestcontainers(
        string nomeProjeto,
        IReadOnlyDictionary<string, ProjetoDeTeste> projetos,
        Dictionary<string, bool> memo,
        HashSet<string> emAndamento)
    {
        if (memo.TryGetValue(nomeProjeto, out bool jaCalculado))
        {
            return jaCalculado;
        }

        if (!projetos.TryGetValue(nomeProjeto, out ProjetoDeTeste? projeto))
        {
            return false;
        }

        if (!emAndamento.Add(nomeProjeto))
        {
            return false; // ciclo defensivo; o grafo de ProjectReference não deveria ciclar
        }

        bool alcanca = projeto.PacotesReferenciados.Any(
                pacote => pacote.StartsWith("Testcontainers", StringComparison.Ordinal))
            || projeto.ProjetosReferenciados.Any(
                referenciado => AlcancaTestcontainers(referenciado, projetos, memo, emAndamento));

        emAndamento.Remove(nomeProjeto);
        memo[nomeProjeto] = alcanca;
        return alcanca;
    }

    private sealed record ProjetoDeTeste(
        string Nome,
        IReadOnlyList<string> PacotesReferenciados,
        IReadOnlyList<string> ProjetosReferenciados);
}
