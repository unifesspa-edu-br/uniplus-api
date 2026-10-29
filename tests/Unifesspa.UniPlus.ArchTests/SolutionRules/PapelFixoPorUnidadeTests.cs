namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Reflection;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using Microsoft.AspNetCore.Authorization;

using Unifesspa.UniPlus.ArchTests.Fixtures;
using Unifesspa.UniPlus.ArchTests.TestSupport;

/// <summary>
/// O acesso é concessão no catálogo, com escopo de unidade (ADR-0078, ADR-0152); papel fixo
/// por unidade, lido do token, não decide nada. A linha de base de <c>[Authorize(Roles)]</c>
/// cobre só o que ainda não migrou; estes testes fecham o resto: os módulos já migrados não
/// declaram papel, e só a autenticação lê o papel do token.
/// </summary>
public sealed partial class PapelFixoPorUnidadeTests
{
    private const string Baseline = "tests/Unifesspa.UniPlus.ArchTests/SolutionRules/AutorizacaoPorPapelBaseline.txt";

    private const string PastaDaAutenticacao = "src/shared/Unifesspa.UniPlus.Infrastructure.Core/Authentication/";

    private static readonly string[] ModulosSemPapel = ["Selecao", "Ingresso", "Autorizacao", "Portal", "Discentes"];

    [Fact(DisplayName = "Fitness_PapelFixoPorUnidadeEmCodigoNovo_Falha")]
    public void Fitness_PapelFixoPorUnidadeEmCodigoNovo_Falha()
    {
        ControllersComPapel([typeof(FixturesDeAutorizacao.PapelFixoDaUnidade)])
            .Should().ContainSingle().Which.Should().Contain("coordenador-ceps");

        LeituraDePapelNoToken("var ok = principal.IsInRole(\"coordenador\");").Should().NotBeEmpty();
        LeituraDePapelNoToken("claims.Add(new Claim(ClaimTypes.Role, papel));").Should().NotBeEmpty();
        LeituraDePapelNoToken("var json = token.GetProperty(\"resource_access\");").Should().NotBeEmpty();
        LeituraDePapelNoToken("policy.RequireRole(\"coordenador\");").Should().NotBeEmpty();
        LeituraDePapelNoToken("// ClaimTypes.Role fica na autenticação").Should().BeEmpty();
        LeituraDePapelNoToken("var cpf = principal.FindFirst(\"cpf\");").Should().BeEmpty();

        ControllersComPapel([.. ModulosSemPapel.SelectMany(static m => ControllersDaSolucao.De(m))]).Should().BeEmpty(
            "os módulos migrados decidem por concessão do catálogo, e não pelo nome de um papel");

        string raiz = SolutionRootLocator.Locate();
        File.ReadAllLines(Path.Join(raiz, Baseline))
            .Where(static l => !string.IsNullOrWhiteSpace(l))
            .Select(static l => l.Split('\t')[0])
            .Where(static caminho => ModulosSemPapel.Any(m => caminho.Contains($"/Unifesspa.UniPlus.{m}.API/", StringComparison.Ordinal)))
            .Should().BeEmpty("a linha de base não abriga módulo que já migrou");

        List<string> leituras = [];
        foreach (string arquivo in Directory.EnumerateFiles(Path.Join(raiz, "src"), "*.cs", SearchOption.AllDirectories))
        {
            string relativo = Path.GetRelativePath(raiz, arquivo).Replace('\\', '/');
            if (relativo.Contains("/obj/", StringComparison.Ordinal)
                || relativo.Contains("/bin/", StringComparison.Ordinal)
                || relativo.StartsWith(PastaDaAutenticacao, StringComparison.Ordinal))
            {
                continue;
            }

            leituras.AddRange(LeituraDePapelNoToken(File.ReadAllText(arquivo)).Select(l => $"{relativo}: {l}"));
        }

        leituras.Should().BeEmpty(
            "só a autenticação lê o papel do token (realm_access, resource_access, ClaimTypes.Role); o resto pergunta ao ponto único de decisão");
    }

    private static List<string> ControllersComPapel(IEnumerable<Type> controllers)
        => [.. controllers.SelectMany(static t => t
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .SelectMany(static m => m.GetCustomAttributes<AuthorizeAttribute>(inherit: true)))
            .Where(static a => !string.IsNullOrEmpty(a.Roles))
            .Select(a => $"{t.FullName}: Roles = \"{a.Roles}\""))];

    private static List<string> LeituraDePapelNoToken(string codigo)
        => [.. LeituraDePapel().Matches(codigo).Select(static m => m.Value.Trim())];

    [GeneratedRegex(@"^(?!\s*//)[^\r\n]*?(\bIsInRole\(|\bRequireRole\(|\bClaimTypes\.Role\b|""realm_access""|""resource_access"")", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex LeituraDePapel();
}
