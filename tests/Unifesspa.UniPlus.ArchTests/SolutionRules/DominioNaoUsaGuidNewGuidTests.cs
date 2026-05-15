namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.IO;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using TestSupport;

/// <summary>
/// Fitness function da ADR-0032: identidade de entidades de domínio é gerada
/// via <see cref="System.Guid.CreateVersion7()"/> (UUID v7), nunca
/// <c>Guid.NewGuid()</c> (v4). Testa por regex em arquivos <c>.cs</c> dos
/// projetos <c>*.Domain</c> — abordagem por inspeção textual é apropriada
/// porque ArchUnitNET enxerga dependência de tipo, não chamadas de método
/// específicas (todas as classes dependem de <c>System.Guid</c> como tipo).
/// </summary>
/// <remarks>
/// Falsos positivos esperáveis e tratados:
/// - Comentários mencionando <c>Guid.NewGuid()</c> historicamente: filtrar
///   linhas que começam com <c>//</c> ou que estão dentro de bloco
///   <c>/* */</c>.
/// - Arquivos gerados em <c>obj/</c> e <c>bin/</c>: excluídos por path.
/// </remarks>
public sealed partial class DominioNaoUsaGuidNewGuidTests
{
    [GeneratedRegex(@"\bGuid\.NewGuid\s*\(\s*\)")]
    private static partial Regex GuidNewGuidPattern();

    [Fact(DisplayName = "ADR-0032: nenhum arquivo .cs em *.Domain chama Guid.NewGuid()")]
    public void Dominio_NaoChama_GuidNewGuid()
    {
        string solutionRoot = SolutionRootLocator.Locate();
        string[] domainSourceRoots =
        [
            Path.Combine(solutionRoot, "src", "shared", "Unifesspa.UniPlus.Kernel"),
            Path.Combine(solutionRoot, "src", "selecao", "Unifesspa.UniPlus.Selecao.Domain"),
            Path.Combine(solutionRoot, "src", "ingresso", "Unifesspa.UniPlus.Ingresso.Domain"),
            Path.Combine(solutionRoot, "src", "portal", "Unifesspa.UniPlus.Portal.Domain"),
            Path.Combine(solutionRoot, "src", "organizacao-institucional", "Unifesspa.UniPlus.OrganizacaoInstitucional.Domain"),
            Path.Combine(solutionRoot, "src", "parametrizacao", "Unifesspa.UniPlus.Parametrizacao.Domain"),
        ];

        List<string> violations = [];

        foreach (string root in domainSourceRoots)
        {
            if (!Directory.Exists(root))
                continue;

            IEnumerable<string> files = Directory
                .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(static p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Where(static p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

            foreach (string file in files)
            {
                string[] lines = File.ReadAllLines(file);
                bool inBlockComment = false;

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    string trimmed = line.TrimStart();

                    // Estado-máquina mínima de comment: rastreia /* ... */ que pode atravessar
                    // múltiplas linhas. Linha que ABRE e FECHA o bloco (/* foo */) sai do estado
                    // dentro do mesmo passo. Não trata corner-cases exóticos como "/* dentro de
                    // string literal" porque arquivos de domínio não fazem parsing-bait.
                    if (inBlockComment)
                    {
                        int closing = line.IndexOf("*/", StringComparison.Ordinal);
                        if (closing < 0)
                            continue;
                        inBlockComment = false;
                        line = line[(closing + 2)..];
                        trimmed = line.TrimStart();
                        if (trimmed.Length == 0)
                            continue;
                    }

                    if (trimmed.StartsWith("//", StringComparison.Ordinal))
                        continue;

                    int blockStart = line.IndexOf("/*", StringComparison.Ordinal);
                    if (blockStart >= 0)
                    {
                        int blockEnd = line.IndexOf("*/", blockStart + 2, StringComparison.Ordinal);
                        if (blockEnd < 0)
                        {
                            inBlockComment = true;
                            line = line[..blockStart];
                        }
                        else
                        {
                            // /* ... */ inline na mesma linha — remove o trecho do comment.
                            line = string.Concat(line.AsSpan(0, blockStart), line.AsSpan(blockEnd + 2));
                        }
                    }

                    if (GuidNewGuidPattern().IsMatch(line))
                    {
                        string relative = Path.GetRelativePath(solutionRoot, file);
                        violations.Add($"{relative}:{i + 1}: {lines[i].Trim()}");
                    }
                }
            }
        }

        violations.Should().BeEmpty(
            "ADR-0032 exige Guid.CreateVersion7() em entidades de domínio para ordenação temporal e localidade B-tree no Postgres. " +
            "Substitua por Guid.CreateVersion7() ou herde de EntityBase, que faz isso por padrão. " +
            $"Violações encontradas:\n  - {string.Join("\n  - ", violations)}");
    }

}
