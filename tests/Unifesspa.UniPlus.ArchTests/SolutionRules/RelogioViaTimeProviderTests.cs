namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.IO;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using TestSupport;

/// <summary>
/// Fitness function da convenção de relógio: todo acesso a tempo de parede em
/// <c>src/</c> passa por um <see cref="System.TimeProvider"/> injetado
/// (<c>TimeProvider.GetUtcNow()</c>), nunca por leituras estáticas diretas
/// <c>DateTime.UtcNow</c>/<c>DateTime.Now</c>/<c>DateTimeOffset.UtcNow</c>/<c>DateTimeOffset.Now</c>.
/// Mantém o domínio determinístico (FakeTimeProvider em testes) e concentra a
/// dependência de relógio nos interceptors/serviços de Infrastructure.
/// </summary>
/// <remarks>
/// <para>
/// Mesma abordagem por inspeção textual de <see cref="DominioNaoUsaGuidNewGuidTests"/>:
/// ArchUnitNET enxerga dependência de tipo, não chamada de membro específico
/// (todas as classes dependem de <c>System.DateTimeOffset</c> como tipo).
/// </para>
/// <para>
/// O escopo é TODO <c>src/</c> (não apenas <c>*.Domain</c>): após a refatoração
/// de relógio, nenhuma camada lê o relógio estático — interceptors, repositórios,
/// schema registry e endpoints smoke recebem <see cref="System.TimeProvider"/>.
/// <c>TimeProvider.System</c> permanece permitido (é o acessor legítimo do
/// relógio real, não uma leitura estática de <c>UtcNow</c>/<c>Now</c>).
/// </para>
/// <para>
/// Falsos positivos tratados: comentários (linha <c>//</c> e bloco <c>/* */</c>)
/// são removidos antes do match; arquivos gerados em <c>obj/</c>/<c>bin/</c> são
/// excluídos por path.
/// </para>
/// </remarks>
public sealed partial class RelogioViaTimeProviderTests
{
    [GeneratedRegex(@"\b(DateTime|DateTimeOffset)\.(UtcNow|Now)\b")]
    private static partial Regex RelogioEstaticoPattern();

    [Fact(DisplayName = "Convenção de relógio: nenhum arquivo .cs em src/ lê DateTime(Offset).UtcNow/.Now")]
    public void Src_NaoLe_RelogioEstatico()
    {
        string solutionRoot = SolutionRootLocator.Locate();
        // Path.Join (não Path.Combine): concatena sem o reset de path rooted,
        // eliminando o risco de descartar silenciosamente solutionRoot.
        string srcRoot = Path.Join(solutionRoot, "src");

        List<string> violations = [];

        IEnumerable<string> files = Directory
            .EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
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
                // dentro do mesmo passo. (Idêntica à de DominioNaoUsaGuidNewGuidTests.)
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

                if (RelogioEstaticoPattern().IsMatch(line))
                {
                    string relative = Path.GetRelativePath(solutionRoot, file);
                    violations.Add($"{relative}:{i + 1}: {lines[i].Trim()}");
                }
            }
        }

        violations.Should().BeEmpty(
            "a convenção de relógio exige TimeProvider injetado (TimeProvider.GetUtcNow()) em src/. "
            + "Injete TimeProvider no construtor/handler/endpoint e use clock.GetUtcNow(); em entidades de "
            + "domínio siga o precedente `TimeProvider? clock = null` e passe (clock ?? TimeProvider.System).GetUtcNow(). "
            + $"Violações encontradas:\n  - {string.Join("\n  - ", violations)}");
    }
}
