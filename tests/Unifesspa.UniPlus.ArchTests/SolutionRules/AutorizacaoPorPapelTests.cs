namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Globalization;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.TestSupport;

/// <summary>
/// A decisão de acesso passa pelas permissões do catálogo (ADR-0078, ADR-0151), não
/// pelo nome do papel. Os <c>[Authorize(Roles = …)]</c> que existiam quando o catálogo
/// nasceu entram como linha de base explícita; ela só diminui, à medida que cada módulo
/// migra, e nunca cresce.
/// </summary>
public sealed partial class AutorizacaoPorPapelTests
{
    private const string Baseline = "tests/Unifesspa.UniPlus.ArchTests/SolutionRules/AutorizacaoPorPapelBaseline.txt";

    [Fact(DisplayName = "Nenhum [Authorize(Roles)] novo além da linha de base, que só diminui")]
    public void AuthorizeComRoles_NaoSaiDaLinhaDeBase()
    {
        string raiz = SolutionRootLocator.Locate();
        Dictionary<string, int> base_ = LerBaseline(Path.Combine(raiz, Baseline));
        Dictionary<string, int> atual = ContarNoCodigo(raiz);

        List<string> novos = [.. atual
            .Where(par => par.Value > base_.GetValueOrDefault(par.Key))
            .Select(par => $"{par.Key}: {par.Value} (linha de base {base_.GetValueOrDefault(par.Key)})")];

        List<string> migrados = [.. base_
            .Where(par => atual.GetValueOrDefault(par.Key) < par.Value)
            .Select(par => $"{par.Key}: {atual.GetValueOrDefault(par.Key)} (linha de base {par.Value})")];

        novos.Should().BeEmpty(
            "o acesso novo se declara com [ExigePermissao<…>] e uma permissão do catálogo, não com o nome de um papel");
        migrados.Should().BeEmpty(
            "o que foi migrado deve sair da linha de base em AutorizacaoPorPapelBaseline.txt, para ela não voltar a crescer");
    }

    private static Dictionary<string, int> LerBaseline(string caminho)
        => File.ReadAllLines(caminho)
            .Where(static linha => !string.IsNullOrWhiteSpace(linha))
            .Select(static linha => linha.Split('\t'))
            .ToDictionary(partes => partes[0], partes => int.Parse(partes[1], CultureInfo.InvariantCulture), StringComparer.Ordinal);

    private static Dictionary<string, int> ContarNoCodigo(string raiz)
    {
        Dictionary<string, int> contagem = new(StringComparer.Ordinal);
        string src = Path.Combine(raiz, "src");

        foreach (string arquivo in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            string relativo = Path.GetRelativePath(raiz, arquivo).Replace('\\', '/');
            if (relativo.Contains("/obj/", StringComparison.Ordinal) || relativo.Contains("/bin/", StringComparison.Ordinal))
            {
                continue;
            }

            int usos = AtributoComRoles().Count(File.ReadAllText(arquivo));
            if (usos > 0)
            {
                contagem[relativo] = usos;
            }
        }

        return contagem;
    }

    [GeneratedRegex(@"^(?!\s*//)[^\r\n]*\bAuthorize\([^)\r\n]*\bRoles\s*=", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex AtributoComRoles();
}
