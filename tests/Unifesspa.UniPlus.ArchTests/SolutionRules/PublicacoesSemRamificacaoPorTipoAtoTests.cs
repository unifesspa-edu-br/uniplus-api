namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.IO;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using TestSupport;

/// <summary>
/// Fitness function da ADR-0103: nenhuma regra do módulo Publicações ramifica
/// comportamento pelo código do tipo de ato. Acrescentar <c>CONVOCACAO</c>,
/// <c>LISTA_ESPERA</c> ou <c>HOMOLOGACAO_ANALISE_DOCUMENTAL</c> deve ser linha de
/// cadastro — jamais um PR no domínio.
/// </summary>
/// <remarks>
/// <para>Os três atributos de consequência (<c>congela_configuracao</c>,
/// <c>unico_por_objeto</c>, <c>efeito_irreversivel</c>) são dados lidos e copiados
/// por valor. Um <c>if (tipo.Codigo == "CONVOCACAO")</c> é a violação: reencarna no
/// documento a ramificação por tipo que o projeto proibiu no processo seletivo.</para>
/// <para>Duas armadilhas que este teste evita, e que a ausência de detecção esconderia:</para>
/// <list type="number">
///   <item>Um diretório inexistente (path errado, projeto renomeado) faria a varredura
///     não ler arquivo nenhum e passar. Por isso a existência dos roots e a contagem
///     mínima de arquivos examinados são <b>asseridas</b>, não pressupostas.</item>
///   <item>Uma consulta de detecção errada nunca acusaria nada. Por isso o detector é
///     alimentado primeiro com uma violação plantada, e só depois de acusá-la é que a
///     sua ausência sobre o código real vale como evidência.</item>
/// </list>
/// <para>Comentários são ignorados: o próprio <c>TipoAtoPublicado</c> cita a violação
/// na sua documentação, e a mensagem de erro de formato usa <c>EDITAL_ABERTURA</c>
/// como exemplo. Nenhum dos dois ramifica coisa alguma — e a mensagem escapa porque a
/// regra procura o literal <b>inteiro</b> em UPPER_SNAKE, não o trecho dentro de uma frase.</para>
/// </remarks>
public sealed partial class PublicacoesSemRamificacaoPorTipoAtoTests
{
    /// <summary>
    /// Literal de string cujo conteúdo inteiro é UPPER_SNAKE — a forma de todo código
    /// de tipo de ato.
    /// </summary>
    /// <remarks>
    /// Deliberadamente <b>independente de sintaxe</b>. Detectar por posição
    /// (<c>== "X"</c>, <c>case "X"</c>) deixa passar o mesmo ramo escrito de outro jeito:
    /// <c>"X" == tipo.Codigo</c>, <c>tipo.Codigo switch { "X" =&gt; … }</c>,
    /// <c>tipo.Codigo is "X"</c>, <c>static readonly string X = "X"</c>. Proibir o
    /// literal em qualquer posição fecha todas de uma vez, e o custo é ter de justificar
    /// um literal legítimo caso apareça — hoje não há nenhum.
    /// </remarks>
    [GeneratedRegex(@"""[A-Z]+(_[A-Z]+)*""")]
    private static partial Regex LiteralDeTipo();

    /// <summary>Literal UPPER_SNAKE dentro de SQL cru — índice ou CHECK filtrando por tipo.</summary>
    [GeneratedRegex(@"'[A-Z]+(_[A-Z]+)*'")]
    private static partial Regex LiteralEmSql();

    private static readonly string[] Camadas =
    [
        "Unifesspa.UniPlus.Publicacoes.Domain",
        "Unifesspa.UniPlus.Publicacoes.Application",
        "Unifesspa.UniPlus.Publicacoes.Infrastructure",
        "Unifesspa.UniPlus.Publicacoes.API",
    ];

    [Theory(DisplayName = "O detector acusa a ramificação escrita de qualquer forma — canários C#")]
    [InlineData(@"if (tipo.Codigo == ""CONVOCACAO"") { return 0.5m; }")]
    [InlineData(@"if (""CONVOCACAO"" == tipo.Codigo) { return 0.5m; }")]
    [InlineData(@"if (tipo.Codigo != ""CONVOCACAO"") { return 0m; }")]
    [InlineData(@"if (tipo.Codigo.Equals(""CONVOCACAO"", StringComparison.Ordinal)) { return 0.5m; }")]
    [InlineData(@"if (string.Equals(""CONVOCACAO"", tipo.Codigo, StringComparison.Ordinal)) { return 0.5m; }")]
    [InlineData(@"switch (tipo.Codigo) { case ""RESULTADO_FINAL"": return 1.0m; }")]
    [InlineData(@"return tipo.Codigo switch { ""CONVOCACAO"" => 0.5m, _ => 0m };")]
    [InlineData(@"if (tipo.Codigo is ""CONVOCACAO"") { return 0.5m; }")]
    [InlineData(@"if (tipo.Codigo is not ""CONVOCACAO"") { return 0m; }")]
    [InlineData(@"private const string Convocacao = ""CONVOCACAO"";")]
    [InlineData(@"private static readonly string Convocacao = ""CONVOCACAO"";")]
    [InlineData(@"private static readonly string[] Congelantes = [""EDITAL_ABERTURA""];")]
    public void Detector_AcusaViolacaoPlantada(string canario)
    {
        ArgumentNullException.ThrowIfNull(canario);

        // Sem esta asserção, a ausência de achados sobre o código real não significaria nada.
        // Cada linha é o mesmo ramo por tipo, escrito de um jeito que uma detecção
        // posicional deixaria passar.
        Violacoes(canario, LiteralDeTipo()).Should().NotBeEmpty();
    }

    [Theory(DisplayName = "O detector não acusa o que não é código de tipo")]
    [InlineData(@"private const string CodigoPattern = ""^[A-Z]+(_[A-Z]+)*$"";")]
    [InlineData(@"private const string ExclusionViolationSqlState = ""23P01"";")]
    [InlineData(@"private const string VigenciaConstraint = ""ex_tipo_ato_publicado_codigo_vigencia"";")]
    [InlineData(@"public const string Schema = ""publicacoes"";")]
    [InlineData(@"return Result.Failure(new DomainError(Codes.CodigoFormato, ""Use UPPER_SNAKE (ex.: EDITAL_ABERTURA)."");")]
    public void Detector_NaoAcusaFalsoPositivo(string trecho)
    {
        ArgumentNullException.ThrowIfNull(trecho);

        // O agregado documenta a violação e a mensagem de erro usa um código como
        // exemplo. Nenhum dos dois ramifica coisa alguma.
        Violacoes(trecho, LiteralDeTipo()).Should().BeEmpty();
    }

    [Fact(DisplayName = "O detector acusa um literal de tipo em SQL cru — canário SQL")]
    public void Detector_AcusaLiteralEmSql()
    {
        const string canario = """
            migrationBuilder.Sql("CREATE INDEX ix ON t (id) WHERE codigo = 'CONVOCACAO';");
            """;

        Violacoes(canario, LiteralEmSql()).Should().ContainSingle();
    }

    [Fact(DisplayName = "ADR-0103: nenhum arquivo de Publicações ramifica por código de tipo de ato")]
    public void Publicacoes_NaoRamificaPorCodigoDeTipo()
    {
        string solutionRoot = SolutionRootLocator.Locate();
        List<string> violacoes = [];
        int arquivosExaminados = 0;

        foreach (string camada in Camadas)
        {
            string root = Path.Combine(solutionRoot, "src", "publicacoes", camada);

            // Um root inexistente faria a varredura passar sem ler nada.
            Directory.Exists(root).Should().BeTrue($"a camada {camada} precisa existir para ser varrida");

            foreach (string arquivo in ArquivosFonte(root))
            {
                arquivosExaminados++;
                string codigo = SemComentarios(File.ReadAllLines(arquivo));

                foreach (Match m in LiteralDeTipo().Matches(codigo))
                {
                    violacoes.Add($"{Path.GetRelativePath(solutionRoot, arquivo)}: {m.Value.Trim()}");
                }
            }
        }

        arquivosExaminados.Should().BeGreaterThan(20, "as quatro camadas juntas têm dezenas de arquivos");

        violacoes.Should().BeEmpty(
            "acrescentar um tipo de ato deve ser linha de cadastro (ADR-0103); "
            + "os três atributos de consequência são dados lidos, nunca ramos de comportamento");
    }

    [Fact(DisplayName = "ADR-0103: nenhuma migration de Publicações filtra por código de tipo de ato")]
    public void Migrations_NaoFiltramPorCodigoDeTipo()
    {
        string solutionRoot = SolutionRootLocator.Locate();
        string root = Path.Combine(
            solutionRoot, "src", "publicacoes",
            "Unifesspa.UniPlus.Publicacoes.Infrastructure", "Persistence", "Migrations");

        Directory.Exists(root).Should().BeTrue("as migrations do módulo precisam existir");

        List<string> violacoes = [];
        int arquivosExaminados = 0;

        foreach (string arquivo in ArquivosFonte(root))
        {
            arquivosExaminados++;
            string codigo = SemComentarios(File.ReadAllLines(arquivo));

            foreach (Match m in LiteralEmSql().Matches(codigo))
            {
                violacoes.Add($"{Path.GetRelativePath(solutionRoot, arquivo)}: {m.Value}");
            }
        }

        arquivosExaminados.Should().BeGreaterThan(0, "há ao menos uma migration no módulo");

        violacoes.Should().BeEmpty(
            "um índice ou CHECK filtrando por literal de tipo tornaria o cadastro dependente de deploy");
    }

    private static IEnumerable<string> ArquivosFonte(string root) =>
        Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(static p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(static p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static IReadOnlyList<string> Violacoes(string codigo, Regex detector) =>
        [.. detector.Matches(SemComentarios(codigo.Split('\n'))).Select(m => m.Value)];

    /// <summary>
    /// Remove comentários de linha e de bloco. A documentação do agregado cita a
    /// violação como exemplo do que é proibido; contá-la seria acusar o texto que
    /// explica a regra.
    /// </summary>
    private static string SemComentarios(IReadOnlyList<string> linhas)
    {
        System.Text.StringBuilder sb = new();
        bool emBloco = false;

        foreach (string linha in linhas)
        {
            string atual = linha;

            if (emBloco)
            {
                int fim = atual.IndexOf("*/", StringComparison.Ordinal);
                if (fim < 0)
                {
                    continue;
                }

                atual = atual[(fim + 2)..];
                emBloco = false;
            }

            int abre = atual.IndexOf("/*", StringComparison.Ordinal);
            if (abre >= 0)
            {
                int fecha = atual.IndexOf("*/", abre + 2, StringComparison.Ordinal);
                if (fecha < 0)
                {
                    emBloco = true;
                    atual = atual[..abre];
                }
                else
                {
                    atual = atual[..abre] + atual[(fecha + 2)..];
                }
            }

            int linhaComentario = atual.IndexOf("//", StringComparison.Ordinal);
            if (linhaComentario >= 0)
            {
                atual = atual[..linhaComentario];
            }

            sb.AppendLine(atual);
        }

        return sb.ToString();
    }
}
