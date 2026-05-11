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
    // Neutraliza CONTEÚDO de strings literais (incluindo escapes \" e
    // verbatim @"..."). Aplicado ANTES do comment scanner para evitar que
    // sequências como "*/*" ou "// foo" dentro de strings confundam a
    // state-machine de block comments. Raw strings (""" ... """) C# 11+
    // não são exploradas hoje no src/ — se vierem a ser, expandir.
    //
    // IMPORTANTE: substituição preserva ASPAS + insere placeholder não-vazio
    // (`_S_`). Substituir por "" (vazio) seria fatal — os 7 patterns do
    // detector exigem literal não-vazio `"[^"]+"`, então strings vazias
    // não casariam e a regra ficaria inativa (Codex P1 original).
    // O placeholder mantém os patterns funcionais: `IsEnvironment("Test")`
    // vira `IsEnvironment("_S_")` que matcha `IsEnvironment\("[^"]+"\)`.
    [GeneratedRegex(@"@""(?:""""|[^""])*""|""(?:\\.|[^""\\])*""")]
    private static partial Regex StringLiteralPattern();

    private const string StringLiteralPlaceholder = "\"_S_\"";

    [GeneratedRegex(@"\bIsEnvironment\s*\(\s*""[^""]+""\s*\)")]
    private static partial Regex IsEnvironmentLiteralPattern();

    [GeneratedRegex(@"\bEnvironmentName\s*==\s*""[^""]+""")]
    private static partial Regex EnvironmentNameEqualsLiteralPattern();

    // Aceita qualifier chain entre `==` e EnvironmentName:
    // `"Test" == env.EnvironmentName` ou `"Test" == builder.Environment.EnvironmentName`.
    // Test-the-test pegou esse buraco no draft inicial.
    [GeneratedRegex(@"""[^""]+""\s*==\s*(?:[\w]+(?:\s*\.\s*[\w]+)*\s*\.\s*)?EnvironmentName\b")]
    private static partial Regex LiteralEqualsEnvironmentNamePattern();

    [GeneratedRegex(@"\bEnvironmentName\.Equals\s*\(\s*""[^""]+""")]
    private static partial Regex EnvironmentNameDotEqualsLiteralPattern();

    // Aceita qualifier chain antes de EnvironmentName: cobre
    // `string.Equals(EnvironmentName, ...)` (acesso bare) e também
    // `string.Equals(builder.Environment.EnvironmentName, ...)` ou
    // `string.Equals(env.EnvironmentName, ...)` — bypass que o regex
    // anterior deixava passar (Codex P2 original).
    [GeneratedRegex(@"\bstring\.Equals\s*\(\s*(?:[\w]+(?:\s*\.\s*[\w]+)*\s*\.\s*)?EnvironmentName\s*,\s*""[^""]+""")]
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
                // Strip CONTEÚDO de strings literais ANTES da state-machine
                // de comments. Caso real coberto: "*/*" em VendorMediaTypeAttribute
                // que, sem o strip, faria o scanner entrar em inBlockComment e
                // ignorar o resto do arquivo. Placeholder não-vazio preserva
                // os patterns do detector funcionais.
                string line = StringLiteralPattern().Replace(originalLine, StringLiteralPlaceholder);
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

    // Test-the-test: prova que os 7 patterns realmente DETECTAM violações
    // canônicas. Sem isso o detector poderia degradar silenciosamente para
    // "passa em qualquer codebase" (regressão real do primeiro draft desta
    // ADR — Codex P1 original onde strip de string deixava patterns sem o
    // que casar). Cada cenário cobre uma das 7 síntaxes banidas.
    [Theory(DisplayName = "ADR-0053: detector captura cada uma das 7 síntaxes banidas e ignora permitidos")]
    [InlineData(@"if (env.IsEnvironment(""Testing""))", true)]
    [InlineData(@"if (builder.Environment.IsEnvironment(""Test""))", true)]
    [InlineData(@"if (env.EnvironmentName == ""Test"")", true)]
    [InlineData(@"if (builder.Environment.EnvironmentName == ""Test"")", true)]
    [InlineData(@"if (""Test"" == env.EnvironmentName)", true)]
    [InlineData(@"if (env.EnvironmentName.Equals(""Test""))", true)]
    [InlineData(@"if (string.Equals(env.EnvironmentName, ""Test"", StringComparison.OrdinalIgnoreCase))", true)]
    [InlineData(@"if (string.Equals(builder.Environment.EnvironmentName, ""Test"", StringComparison.OrdinalIgnoreCase))", true)]
    [InlineData(@"if (env.EnvironmentName is ""Test"")", true)]
    [InlineData(@"switch (env.EnvironmentName) { case ""Test"": break; }", true)]
    // Permitidos (não devem casar):
    [InlineData(@"if (env.IsDevelopment())", false)]
    [InlineData(@"if (env.IsProduction())", false)]
    [InlineData(@"if (!env.IsDevelopment())", false)]
    [InlineData(@"_logger.LogInformation(""Env={Env}"", env.EnvironmentName);", false)]
    // String literal contendo /* não deve confundir detector nem dar falso positivo:
    [InlineData(@"public string MediaType { get; } = ""*/*"";", false)]
    [InlineData(@"// IsEnvironment(""Testing"") em comentário não conta", false)]
    public void Detector_Captura_Sintaxes_Banidas_Ignora_Permitidos(string codeLine, bool deveMatchar)
    {
        // Replicar o pré-processamento exato do scanner real (strip string
        // literais antes de aplicar patterns), para evitar divergência
        // entre o que o test prova e o que o detector roda em src/.
        string preprocessed = StringLiteralPattern().Replace(codeLine, StringLiteralPlaceholder);

        // Linhas iniciadas por // são filtradas pela state-machine no
        // detector real — replicar aqui para o caso de comentário.
        bool inComment = preprocessed.TrimStart().StartsWith("//", StringComparison.Ordinal);

        bool matched = !inComment && (
            IsEnvironmentLiteralPattern().IsMatch(preprocessed)
            || EnvironmentNameEqualsLiteralPattern().IsMatch(preprocessed)
            || LiteralEqualsEnvironmentNamePattern().IsMatch(preprocessed)
            || EnvironmentNameDotEqualsLiteralPattern().IsMatch(preprocessed)
            || StringEqualsEnvironmentNameLiteralPattern().IsMatch(preprocessed)
            || EnvironmentNameIsPatternLiteral().IsMatch(preprocessed)
            || SwitchOverEnvironmentNamePattern().IsMatch(preprocessed));

        matched.Should().Be(deveMatchar,
            $"Linha `{codeLine}` deveria {(deveMatchar ? "" : "NÃO ")}matchar algum dos 7 patterns. " +
            $"Pré-processado: `{preprocessed}`");
    }
}
