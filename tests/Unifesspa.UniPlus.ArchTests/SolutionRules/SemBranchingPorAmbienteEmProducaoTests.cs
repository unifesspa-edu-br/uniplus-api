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
    // Neutraliza CONTEÚDO de strings literais. Cobre:
    //   - regulares "..." (com escapes \")
    //   - verbatim @"..." (com "" como escape)
    //   - interpolated $"..." e $@"..." / @$"..."
    //   - raw strings C# 11+ """...""" (single-line; multi-linha via Singleline)
    // Aplicado ANTES do comment scanner para evitar que sequências como
    // "*/*" ou "// foo" dentro de strings confundam a state-machine de
    // block comments. Cobre P1.A do Codex review (interpolated/raw bypass).
    //
    // IMPORTANTE: substituição preserva ASPAS + insere placeholder não-vazio
    // (`_S_`). Substituir por "" (vazio) seria fatal — os 7 patterns do
    // detector exigem literal não-vazio `"[^"]+"`, então strings vazias
    // não casariam e a regra ficaria inativa.
    [GeneratedRegex(@"""""""[\s\S]*?""""""|\$?@""(?:""""|[^""])*""|@\$""(?:""""|[^""])*""|\$?""(?:\\.|[^""\\])*""")]
    private static partial Regex StringLiteralPattern();

    private const string StringLiteralPlaceholder = "\"_S_\"";

    [GeneratedRegex(@"\bIsEnvironment\s*\(\s*""[^""]+""\s*\)")]
    private static partial Regex IsEnvironmentLiteralPattern();

    // Variante com named argument: `IsEnvironment(environmentName: "Test")`.
    // C# aceita ambos os nomes do parâmetro `environmentName` (nome real do
    // overload) ou positional. Codex 4ª rodada P2.
    [GeneratedRegex(@"\bIsEnvironment\s*\(\s*environmentName\s*:\s*""[^""]+""")]
    private static partial Regex IsEnvironmentNamedArgPattern();

    [GeneratedRegex(@"\bEnvironmentName\s*==\s*""[^""]+""")]
    private static partial Regex EnvironmentNameEqualsLiteralPattern();

    // Operador `!=`: semanticamente equivalente ao `==` (apenas inverte o
    // branch). Codex 4ª rodada P2. Mirror simétrico dos 2 patterns `==`.
    [GeneratedRegex(@"\bEnvironmentName\s*!=\s*""[^""]+""")]
    private static partial Regex EnvironmentNameNotEqualsLiteralPattern();

    [GeneratedRegex(@"""[^""]+""\s*!=\s*(?:[\w]+(?:\s*\.\s*[\w]+)*\s*\.\s*)?EnvironmentName\b")]
    private static partial Regex LiteralNotEqualsEnvironmentNamePattern();

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

    // Variante reversa: argumentos invertidos `string.Equals("Test", env.EnvironmentName, ...)`.
    // Codex 3ª rodada P2 — `string.Equals` aceita os dois operandos em qualquer
    // ordem semanticamente; o detector precisa cobrir ambos.
    [GeneratedRegex(@"\bstring\.Equals\s*\(\s*""[^""]+""\s*,\s*(?:[\w]+(?:\s*\.\s*[\w]+)*\s*\.\s*)?EnvironmentName\b")]
    private static partial Regex StringEqualsLiteralEnvironmentNamePattern();

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
            string[] processed = new string[lines.Length];
            bool inBlockComment = false;

            // Pass 1: strip strings + comments por linha (state-machine
            // mantém estado /*..*/ cross-line). Mesma lógica de
            // DominioNaoUsaGuidNewGuidTests para preservar false-positives
            // em comentários históricos.
            for (int i = 0; i < lines.Length; i++)
            {
                string line = StringLiteralPattern().Replace(lines[i], StringLiteralPlaceholder);

                if (inBlockComment)
                {
                    int closing = line.IndexOf("*/", StringComparison.Ordinal);
                    if (closing < 0)
                    {
                        processed[i] = string.Empty;
                        continue;
                    }
                    inBlockComment = false;
                    line = line[(closing + 2)..];
                }

                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal))
                {
                    processed[i] = string.Empty;
                    continue;
                }

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

                int singleComment = line.IndexOf("//", StringComparison.Ordinal);
                if (singleComment >= 0)
                    line = line[..singleComment];

                processed[i] = line;
            }

            // Pass 2: regex global no arquivo processado para capturar
            // chamadas multi-linha (Codex P1.B). Newlines viram \n no
            // joined; `\s*` nos regex matcha através de newline naturalmente.
            // Cada match → linha original via Count('\n') no índice.
            string joined = string.Join('\n', processed);

            CollectMatches(joined, IsEnvironmentLiteralPattern(), lines, file, solutionRoot, violations);
            CollectMatches(joined, IsEnvironmentNamedArgPattern(), lines, file, solutionRoot, violations);
            CollectMatches(joined, EnvironmentNameEqualsLiteralPattern(), lines, file, solutionRoot, violations);
            CollectMatches(joined, EnvironmentNameNotEqualsLiteralPattern(), lines, file, solutionRoot, violations);
            CollectMatches(joined, LiteralEqualsEnvironmentNamePattern(), lines, file, solutionRoot, violations);
            CollectMatches(joined, LiteralNotEqualsEnvironmentNamePattern(), lines, file, solutionRoot, violations);
            CollectMatches(joined, EnvironmentNameDotEqualsLiteralPattern(), lines, file, solutionRoot, violations);
            CollectMatches(joined, StringEqualsEnvironmentNameLiteralPattern(), lines, file, solutionRoot, violations);
            CollectMatches(joined, StringEqualsLiteralEnvironmentNamePattern(), lines, file, solutionRoot, violations);
            CollectMatches(joined, EnvironmentNameIsPatternLiteral(), lines, file, solutionRoot, violations);
            CollectMatches(joined, SwitchOverEnvironmentNamePattern(), lines, file, solutionRoot, violations);
        }

        violations.Should().BeEmpty(
            "ADR-0053 bane branching por string de ambiente em src/. " +
            "Substitua por IsDevelopment()/IsProduction() em composition root (válido para HSTS/Swagger/validation guards) " +
            "ou mova a customização para tests/Unifesspa.UniPlus.IntegrationTests.Fixtures/Hosting/ApiFactoryBase.cs. " +
            "HML/sanidade são semanticamente Production — Vault injeta config. " +
            $"Violações encontradas:\n  - {string.Join("\n  - ", violations)}");
    }

    private static void CollectMatches(
        string joined,
        Regex pattern,
        string[] originalLines,
        string filePath,
        string solutionRoot,
        List<string> violations)
    {
        foreach (Match match in pattern.Matches(joined))
        {
            // Index → line number: conta '\n' antes do match.
            int lineNumber = 1;
            for (int j = 0; j < match.Index; j++)
            {
                if (joined[j] == '\n')
                    lineNumber++;
            }

            string relative = Path.GetRelativePath(solutionRoot, filePath);
            string original = lineNumber <= originalLines.Length
                ? originalLines[lineNumber - 1].Trim()
                : "<sem linha original>";
            violations.Add($"{relative}:{lineNumber}: {original}");
        }
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
    [InlineData(@"if (string.Equals(""Test"", env.EnvironmentName, StringComparison.OrdinalIgnoreCase))", true)]
    [InlineData(@"if (string.Equals(""Test"", builder.Environment.EnvironmentName, StringComparison.OrdinalIgnoreCase))", true)]
    // Named argument syntax (Codex 4ª rodada P2.A):
    [InlineData(@"if (env.IsEnvironment(environmentName: ""Test""))", true)]
    // Operador != (Codex 4ª rodada P2.B):
    [InlineData(@"if (env.EnvironmentName != ""Test"")", true)]
    [InlineData(@"if (builder.Environment.EnvironmentName != ""Test"")", true)]
    [InlineData(@"if (""Test"" != env.EnvironmentName)", true)]
    [InlineData(@"if (env.EnvironmentName is ""Test"")", true)]
    [InlineData(@"switch (env.EnvironmentName) { case ""Test"": break; }", true)]
    // Bypass por interpolated/raw strings (Codex P1.A) — placeholder coverage:
    [InlineData(@"if (env.IsEnvironment($""Test""))", true)]
    [InlineData(@"if (env.IsEnvironment(""""""Test""""""))", true)]
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
            || IsEnvironmentNamedArgPattern().IsMatch(preprocessed)
            || EnvironmentNameEqualsLiteralPattern().IsMatch(preprocessed)
            || EnvironmentNameNotEqualsLiteralPattern().IsMatch(preprocessed)
            || LiteralEqualsEnvironmentNamePattern().IsMatch(preprocessed)
            || LiteralNotEqualsEnvironmentNamePattern().IsMatch(preprocessed)
            || EnvironmentNameDotEqualsLiteralPattern().IsMatch(preprocessed)
            || StringEqualsEnvironmentNameLiteralPattern().IsMatch(preprocessed)
            || StringEqualsLiteralEnvironmentNamePattern().IsMatch(preprocessed)
            || EnvironmentNameIsPatternLiteral().IsMatch(preprocessed)
            || SwitchOverEnvironmentNamePattern().IsMatch(preprocessed));

        matched.Should().Be(deveMatchar,
            $"Linha `{codeLine}` deveria {(deveMatchar ? "" : "NÃO ")}matchar algum dos 7 patterns. " +
            $"Pré-processado: `{preprocessed}`");
    }
}
