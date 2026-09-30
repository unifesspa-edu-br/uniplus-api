namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using AwesomeAssertions;

/// <summary>
/// Fitness tests do catálogo <c>rol_de_fatos_candidato</c> (UNI-REQ-0077, ADR-0136): o
/// catálogo é cadastrado pelo administrador e consumido cross-módulo por leitor síncrono, sem FK
/// cross-schema (ADR-0061). Estas regras travam duas fronteiras da decisão diretamente sobre o
/// código-fonte:
/// <list type="bullet">
///   <item><description>toda rota de escrita do catálogo exige o papel
///   <c>plataforma-admin</c>;</description></item>
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

    [Fact(DisplayName = "Toda escrita no catálogo de fatos exige o papel plataforma-admin")]
    public void Controller_EscritaSoDoAdministrador()
    {
        string controller = CaminhoDoController();
        File.Exists(controller).Should().BeTrue($"o controller do catálogo vive em {controller}");

        // Comentários C# removidos antes do match: uma nota que cite um verbo não pode ser lida
        // como uma rota real. Cada verbo de escrita tem de vir acompanhado da exigência do papel
        // no mesmo bloco de atributos (ADR-0136: o catálogo é cadastrado pelo administrador).
        string codigo = SemComentarios(controller);
        MatchCollection verbos = VerboDeEscrita.Matches(codigo);
        verbos.Should().NotBeEmpty("o administrador cadastra e mantém o catálogo por HTTP");
        foreach (Match verbo in verbos)
        {
            string blocoDeAtributos = codigo.Substring(verbo.Index, Math.Min(200, codigo.Length - verbo.Index));
            blocoDeAtributos.Should().Contain("[Authorize(Roles = \"plataforma-admin\")]",
                $"a rota de escrita em {verbo.Value} é exclusiva do administrador");
        }
    }

    [Fact(DisplayName = "Nenhuma migration de outro módulo cria FK cross-schema apontando para rol_de_fatos_candidato")]
    public void Migrations_SemFkCrossSchemaParaFatoCandidato()
    {
        string src = RaizSrc();
        Directory.Exists(src).Should().BeTrue($"a árvore de código vive em {src}");

        // A FK de fato_valor_dominio → rol_de_fatos_candidato (ADR-0116) é
        // intra-schema — filho e pai vivem no mesmo módulo/DbContext Configuracao.
        // A regra proíbe é OUTRO módulo (Selecao, Ingresso) referenciar o catálogo
        // por FK cross-schema; a referência cross-módulo é sempre por valor
        // (snapshot-copy, ADR-0061).
        List<string> infratoras = Directory
            .EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(arquivo => arquivo.Contains("Migrations", StringComparison.Ordinal))
            .Where(arquivo => !arquivo.Contains(
                Path.Join("configuracao", "Unifesspa.UniPlus.Configuracao.Infrastructure"), StringComparison.Ordinal))
            .Where(arquivo => FkParaFatoCandidato.IsMatch(SemComentarios(arquivo)))
            .Select(arquivo => Path.GetFileName(arquivo)!)
            .ToList();

        infratoras.Should().BeEmpty(
            "a referência cross-módulo ao catálogo é por valor (ADR-0061), nunca por FK cross-schema. "
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
