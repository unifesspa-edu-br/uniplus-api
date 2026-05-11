namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.IO;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using TestSupport;

/// <summary>
/// Fitness function da ADR-0053: código de produção em <c>src/</c> não pode
/// branchear comportamento por strings literais de ambiente.
/// </summary>
/// <remarks>
/// <para>
/// <b>Banido</b> (cobre as 6 síntaxes do antipattern):
/// <list type="bullet">
///   <item><c>IHostEnvironment.IsEnvironment("...")</c> com argumento literal</item>
///   <item><c>EnvironmentName == "..."</c> com literal — qualquer lado</item>
///   <item><c>EnvironmentName.Equals("...")</c> com literal</item>
///   <item><c>string.Equals(EnvironmentName, "...", ...)</c> com literal</item>
///   <item><c>EnvironmentName is "..."</c> (pattern matching)</item>
///   <item><c>switch (...EnvironmentName...)</c> com cases literais</item>
/// </list>
/// </para>
/// <para>
/// <b>Permitido</b> (não match): <c>IsDevelopment()</c>, <c>IsProduction()</c>,
/// e <c>EnvironmentName</c> como valor read-only em log/OTel attribute.
/// </para>
/// <para>
/// Abordagem por inspeção textual com pré-processamento que strip strings
/// literais antes do comment scanner. ArchUnitNET enxerga dependência de
/// tipo, não chamada de método com argumento literal — <c>IsEnvironment(string)</c>
/// e <c>EnvironmentName == "..."</c> não são analisáveis por dependency rules.
/// </para>
/// <para>
/// Falsos positivos esperáveis e tratados:
/// <list type="bullet">
///   <item>Strings literais contendo <c>/*</c>, <c>*/</c>, <c>//</c> (ex.:
///   <c>"*/*"</c> em <c>VendorMediaTypeAttribute</c>): neutralizadas antes do
///   comment scanner via <see cref="StripStringLiteralsPattern"/></item>
///   <item>Comentários mencionando os patterns historicamente: state-machine de
///   block comments + filtro de linhas iniciadas por <c>//</c></item>
///   <item>Arquivos gerados em <c>obj/</c>, <c>bin/</c> e <c>*.g.cs</c>:
///   excluídos por path/extensão</item>
/// </list>
/// </para>
/// </remarks>
public sealed partial class SemBranchingPorAmbienteEmProducaoTests
{
    // Neutraliza strings literais "double-quoted" (incluindo escapes \") e
    // verbatim @"...". Aplicado ANTES do comment scanner para evitar que
    // sequências como "*/*" ou "// foo" dentro de strings confundam o
    // detector de comments. Raw strings (""" ... """) C# 11+ não são
    // exploradas hoje no src/ — se vierem a ser, expandir.
    [GeneratedRegex(@"@""(?:""""|[^""])*""|""(?:\\.|[^""\\])*""")]
    private static partial Regex StringLiteralPattern();

    [GeneratedRegex(@"\bIsEnvironment\s*\(\s*""[^""]+""\s*\)")]
    private static partial Regex IsEnvironmentLiteralPattern();

    [GeneratedRegex(@"\bEnvironmentName\s*==\s*""[^""]+""")]
    private static partial Regex EnvironmentNameEqualsLiteralPattern();

    [GeneratedRegex(@"""[^""]+""\s*==\s*\bEnvironmentName\b")]
    private static partial Regex LiteralEqualsEnvironmentNamePattern();

    [GeneratedRegex(@"\bEnvironmentName\.Equals\s*\(\s*""[^""]+""")]
    private static partial Regex EnvironmentNameDotEqualsLiteralPattern();

    [GeneratedRegex(@"\bstring\.Equals\s*\(\s*EnvironmentName\s*,\s*""[^""]+""")]
    private static partial Regex StringEqualsEnvironmentNameLiteralPattern();

    [GeneratedRegex(@"\bEnvironmentName\s+is\s+""[^""]+""")]
    private static partial Regex EnvironmentNameIsPatternLiteral();

    [GeneratedRegex(@"\bswitch\s*\([^)]*\bEnvironmentName\b")]
    private static partial Regex SwitchOverEnvironmentNamePattern();

    [Fact(DisplayName = "ADR-0053: nenhum arquivo .cs em src/ contém IsEnvironment(literal) ou EnvironmentName == literal")]
    public void Producao_NaoBrancheia_PorStringDeAmbiente()
    {
        string solutionRoot = SolutionRootLocator.Locate();
        string srcRoot = Path.Combine(solutionRoot, "src");

        Directory.Exists(srcRoot).Should().BeTrue(
            $"src/ deve existir a partir de SolutionRootLocator ({srcRoot})");

        List<string> violations = [];

        IEnumerable<string> files = Directory
            .EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(static p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(static p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(static p => !p.EndsWith(".g.cs", StringComparison.Ordinal))
            .Where(static p => !p.EndsWith(".Designer.cs", StringComparison.Ordinal));

        foreach (string file in files)
        {
            string[] lines = File.ReadAllLines(file);
            bool inBlockComment = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string originalLine = lines[i];
                // Strip strings literais ANTES de aplicar a state-machine de
                // comments. Caso real coberto: "*/*" em VendorMediaTypeAttribute
                // que, sem o strip, faria o scanner entrar em inBlockComment e
                // ignorar o resto do arquivo (Codex P1).
                string line = StringLiteralPattern().Replace(originalLine, "\"\"");
                string trimmed = line.TrimStart();

                // State-machine mínima de comment: rastreia /* ... */ multi-linha.
                // Mesma lógica usada em DominioNaoUsaGuidNewGuidTests para preservar
                // false-positives historicamente documentados em ADRs/comentários.
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
                        line = string.Concat(line.AsSpan(0, blockStart), line.AsSpan(blockEnd + 2));
                    }
                }

                if (IsEnvironmentLiteralPattern().IsMatch(line)
                    || EnvironmentNameEqualsLiteralPattern().IsMatch(line)
                    || LiteralEqualsEnvironmentNamePattern().IsMatch(line)
                    || EnvironmentNameDotEqualsLiteralPattern().IsMatch(line)
                    || StringEqualsEnvironmentNameLiteralPattern().IsMatch(line)
                    || EnvironmentNameIsPatternLiteral().IsMatch(line)
                    || SwitchOverEnvironmentNamePattern().IsMatch(line))
                {
                    string relative = Path.GetRelativePath(solutionRoot, file);
                    violations.Add($"{relative}:{i + 1}: {originalLine.Trim()}");
                }
            }
        }

        violations.Should().BeEmpty(
            "ADR-0053 bane branching por string de ambiente em src/. " +
            "Substitua por IsDevelopment()/IsProduction() em composition root (válido para HSTS/Swagger/validation guards) " +
            "ou mova a customização para tests/Unifesspa.UniPlus.IntegrationTests.Fixtures/Hosting/ApiFactoryBase.cs. " +
            "HML/sanidade são semanticamente Production — Vault injeta config. " +
            $"Violações encontradas:\n  - {string.Join("\n  - ", violations)}");
    }
}
