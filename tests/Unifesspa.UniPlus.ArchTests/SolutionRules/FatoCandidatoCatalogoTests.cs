namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using AwesomeAssertions;

/// <summary>
/// Fitness tests do catálogo <c>rol_de_fatos_candidato</c> (UNI-REQ-0077, ADR-0111): o
/// vocabulário é seed-governado e append-only, consumido cross-módulo por leitor
/// síncrono, sem FK cross-schema (ADR-0061). Estas regras travam duas fronteiras
/// da decisão diretamente sobre o código-fonte:
/// <list type="bullet">
///   <item><description>o controller do catálogo não expõe rota de escrita —
///   adicionar um fato é um PR de desenvolvimento (seed + código de resolução),
///   nunca uma operação de tela;</description></item>
///   <item><description>nenhuma migration de qualquer módulo cria uma chave
///   estrangeira apontando para <c>rol_de_fatos_candidato</c> — a referência cross-módulo
///   é por valor (snapshot-copy), não por FK cross-schema.</description></item>
/// </list>
/// </summary>
public sealed class FatoCandidatoCatalogoTests
{
    /// <summary>
    /// Detecta um verbo HTTP de escrita (<c>HttpPost</c>/<c>HttpPut</c>/
    /// <c>HttpPatch</c>/<c>HttpDelete</c>) como atributo de ação.
    /// </summary>
    private static readonly Regex VerboDeEscrita = new(
        @"\[Http(Post|Put|Patch|Delete)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Detecta uma FK cuja tabela-alvo é <c>rol_de_fatos_candidato</c> em qualquer
    /// migration (<c>principalTable: "rol_de_fatos_candidato"</c>).
    /// </summary>
    private static readonly Regex FkParaFatoCandidato = new(
        "principalTable:\\s*\"rol_de_fatos_candidato\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [Fact(DisplayName = "O controller do catálogo de fatos não declara verbo HTTP de escrita")]
    public void Controller_SomenteLeitura()
    {
        string controller = CaminhoDoController();
        File.Exists(controller).Should().BeTrue($"o controller do catálogo vive em {controller}");

        // Comentários C# removidos antes do match: uma nota que cite um verbo não
        // pode ser lida como uma rota real.
        VerboDeEscrita.IsMatch(SemComentarios(controller)).Should().BeFalse(
            "o catálogo rol_de_fatos_candidato é seed-governado — nenhum POST/PUT/PATCH/DELETE o edita por HTTP");
    }

    [Fact(DisplayName = "Nenhuma migration cria FK cross-schema apontando para rol_de_fatos_candidato")]
    public void Migrations_SemFkParaFatoCandidato()
    {
        string src = RaizSrc();
        Directory.Exists(src).Should().BeTrue($"a árvore de código vive em {src}");

        List<string> infratoras = Directory
            .EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(arquivo => arquivo.Contains("Migrations", StringComparison.Ordinal))
            .Where(arquivo => FkParaFatoCandidato.IsMatch(SemComentarios(arquivo)))
            .Select(arquivo => Path.GetFileName(arquivo)!)
            .ToList();

        infratoras.Should().BeEmpty(
            "a referência ao catálogo é por valor (ADR-0061), nunca por FK cross-schema. "
            + $"Migrations com FK para rol_de_fatos_candidato: {string.Join(", ", infratoras)}");
    }

    [Theory(DisplayName = "O detector de verbo de escrita reconhece as formas de atributo")]
    [InlineData("[HttpPost(\"admin/x\")]")]
    [InlineData("[HttpPut(\"admin/x/{id}\")]")]
    [InlineData("[HttpDelete]")]
    [InlineData("[HttpPatch]")]
    public void DetectorEscrita_ReconheceFormas(string linha) =>
        VerboDeEscrita.IsMatch(linha).Should().BeTrue();

    [Theory(DisplayName = "O detector de verbo de escrita não confunde leitura com escrita")]
    [InlineData("[HttpGet(\"fatos-candidato\")]")]
    [InlineData("[HttpGet(\"fatos-candidato/{codigo}\")]")]
    public void DetectorEscrita_NaoAcusaLeitura(string linha) =>
        VerboDeEscrita.IsMatch(linha).Should().BeFalse();

    private static string SemComentarios(string arquivo) => string.Join(
        '\n',
        File.ReadLines(arquivo).Where(static linha => !linha.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    private static string CaminhoDoController() => Path.GetFullPath(Path.Join(
        RaizSrc(),
        "configuracao",
        "Unifesspa.UniPlus.Configuracao.API",
        "Controllers",
        "FatosCandidatoController.cs"));

    private static string RaizSrc([CallerFilePath] string origem = "") =>
        Path.GetFullPath(Path.Join(
            Path.GetDirectoryName(origem)!,
            "..",
            "..",
            "..",
            "src"));
}
