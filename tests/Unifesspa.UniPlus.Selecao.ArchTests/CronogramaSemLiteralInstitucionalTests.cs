namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using AwesomeAssertions;

/// <summary>
/// CA-19 (asserção de AUSÊNCIA, Story #851): nenhum tipo de validação/gate do
/// cronograma conhece nome de conselho, órgão ou instância — a suspensividade é
/// configuração (dois pares anuláveis), nunca <c>if (modulo == Ingresso)</c> nem
/// <c>if (fase.UnidadeResponsavelSigla == "CEPS")</c>.
/// </summary>
/// <remarks>
/// <para>
/// O detector varre <b>literais de string em posição de comparação</b> — <c>==</c>,
/// <c>case "..."</c>, <c>.Equals("...")</c> — não nomes de propriedade, tipo ou
/// contrato. <c>SuspensividadePrimeiraInstancia</c>/<c>SuspensividadeSegundaInstancia</c>
/// são identificadores (nunca aparecem entre aspas em posição de comparação) e não
/// disparam o detector.
/// </para>
/// <para>
/// <b>O canário vem primeiro.</b> Antes de confiar que a ausência de acusação no código
/// real do gate significa alguma coisa, este teste planta deliberadamente um literal de
/// comparação institucional e prova que o detector o acusa — um fitness que nunca dispara
/// não prova nada.
/// </para>
/// </remarks>
public sealed class CronogramaSemLiteralInstitucionalTests
{
    /// <summary>Nomes de órgão/conselho — nenhum pode aparecer em posição de comparação no gate.</summary>
    private static readonly string[] TermosBanidos =
        ["CONSEPE", "CONSUN", "CEPS", "CGA", "MEC", "SIGAA"];

    /// <summary>"instancia"/"instância" (com ou sem acento, qualquer capitalização) como literal comparado.</summary>
    private static readonly Regex LiteralDeInstancia = new(
        "inst[aâ]nci", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>Comparação por igualdade: <c>== "X"</c> ou <c>"X" ==</c>.</summary>
    private static readonly Regex ComparacaoIgualdade = new(
        """(==\s*"([^"]*)")|("([^"]*)"\s*==)""",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary><c>switch</c>/<c>case</c> sobre literal: <c>case "X":</c>.</summary>
    private static readonly Regex ComparacaoCase = new(
        """case\s*"([^"]*)"\s*:""",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary><c>.Equals("X")</c>.</summary>
    private static readonly Regex ComparacaoEquals = new(
        """\.Equals\(\s*"([^"]*)"\s*\)""",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>Os arquivos do gate do cronograma (Domain + Application), além de <c>ProcessoSeletivo.cs</c> e da pasta das regras do processo.</summary>
    private static readonly string[] ArquivosDoGate =
    [
        "FaseCronograma.cs",
        "RegraRecursoFase.cs",
        "BancaRequerida.cs",
        "DefinirCronogramaFasesCommand.cs",
        "DefinirCronogramaFasesCommandHandler.cs",
        "DefinirCronogramaFasesCommandValidator.cs",
        "MontagemDasFases.cs",
        "MontagemDosProdutosDaFase.cs",
        "MontagemDasBancasDaFase.cs",
        "MontagemDaRegraDeRecursoDaFase.cs",
        "ConferenciaDaAncoraDoRecurso.cs",
        "CadastrosDasFases.cs",
        "ConferenciaDoProdutoDoAto.cs",
        "AtosConsultadosNaEdicao.cs",
        "InterrupcaoDaFase.cs",
    ];

    [Fact(DisplayName = "Nenhum literal de comparação institucional no gate do cronograma")]
    public void Gate_NaoContemLiteralInstitucional()
    {
        List<string> achados = [];
        List<string> arquivos = [.. ArquivosExplicitosDoGate(), .. ArquivosDasRegrasDoProcesso()];

        foreach (string caminho in arquivos)
        {
            string codigo = SemComentarios(caminho);
            foreach (string literal in LiteraisComparados(codigo))
            {
                if (EhTermoBanido(literal))
                {
                    achados.Add($"{Path.GetFileName(caminho)}: \"{literal}\"");
                }
            }
        }

        achados.Should().BeEmpty(
            "o gate do cronograma não pode conhecer nome de conselho, órgão ou instância — a unidade responsável e a " +
            $"suspensividade são dado declarado, nunca literal em código (CA-19). Achados: {string.Join(", ", achados)}");
    }

    [Fact(DisplayName = "Todo arquivo explícito do gate existe e a pasta das regras do processo não está vazia")]
    public void Gate_VarreOQueDeclara()
    {
        ArquivosExplicitosDoGate().Where(static caminho => !File.Exists(caminho)).Should().BeEmpty(
            "um arquivo do gate que mudou de nome ou de lugar deixaria de ser varrido sem que ninguém notasse");
        ArquivosDasRegrasDoProcesso().Should().NotBeEmpty(
            "a pasta das regras do processo é varrida inteira, e uma pasta vazia ou renomeada tiraria a cobertura");
    }

    [Theory(DisplayName = "O detector ACUSA um literal de comparação institucional plantado (canário obrigatório)")]
    [InlineData("""if (regra.Codigo == "CONSEPE") { return true; }""")]
    [InlineData("""if ("CONSUN" == orgao) { return true; }""")]
    [InlineData("""switch (dono) { case "CEPS": return true; default: return false; }""")]
    [InlineData("""if (fase.UnidadeResponsavelSigla.Equals("CGA")) { return true; }""")]
    [InlineData("""if (rotulo == "instancia_superior") { return true; }""")]
    public void Detector_AcusaCanarioPlantado(string trechoPlantado)
    {
        bool acusado = LiteraisComparados(trechoPlantado).Any(EhTermoBanido);

        acusado.Should().BeTrue(
            "o detector precisa acusar um literal de comparação institucional plantado deliberadamente — sem essa " +
            $"prova, a ausência de acusação no código real do gate não significa nada. Trecho plantado: {trechoPlantado}");
    }

    [Theory(DisplayName = "O detector NÃO acusa identificador, comentário ou valor fora de posição de comparação (falso positivo)")]
    [InlineData("public decimal? SuspensividadePrimeiraInstanciaValor { get; private set; }")]
    [InlineData("public UnidadePrazo? SuspensividadeSegundaInstanciaUnidade { get; private set; }")]
    [InlineData("public string? UnidadeResponsavelSigla { get; private set; }")]
    [InlineData("""unidadeResponsavelSigla: "CEPS",""")]
    [InlineData("/// <summary>A suspensividade da 1ª instância...</summary>")]
    public void Detector_NaoAcusaFalsoPositivo(string trecho)
    {
        bool acusado = LiteraisComparados(trecho).Any(EhTermoBanido);

        acusado.Should().BeFalse(
            $"identificadores, comentários/xmldoc e valores passados como argumento (fora de == / case / .Equals) " +
            $"não são literais de comparação institucional. Trecho: {trecho}");
    }

    private static bool EhTermoBanido(string literal) =>
        TermosBanidos.Contains(literal, StringComparer.OrdinalIgnoreCase) || LiteralDeInstancia.IsMatch(literal);

    private static IEnumerable<string> LiteraisComparados(string codigo)
    {
        foreach (Match m in ComparacaoIgualdade.Matches(codigo))
        {
            yield return m.Groups[2].Success ? m.Groups[2].Value : m.Groups[4].Value;
        }

        foreach (Match m in ComparacaoCase.Matches(codigo))
        {
            yield return m.Groups[1].Value;
        }

        foreach (Match m in ComparacaoEquals.Matches(codigo))
        {
            yield return m.Groups[1].Value;
        }
    }

    /// <summary>Remove comentários de linha — uma nota explicando a AUSÊNCIA de um literal não pode ser lida como sua presença (mesmo padrão de <see cref="RolDeRegrasSemGatilhoTests"/>).</summary>
    private static string SemComentarios(string arquivo) => string.Join(
        '\n',
        File.ReadLines(arquivo).Where(static linha => !linha.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    private static IEnumerable<string> ArquivosExplicitosDoGate()
    {
        string raizDomain = Caminho("src", "selecao", "Unifesspa.UniPlus.Selecao.Domain", "Entities");
        string raizApplication = Caminho("src", "selecao", "Unifesspa.UniPlus.Selecao.Application", "Commands", "ProcessosSeletivos");
        string raizValidators = Caminho("src", "selecao", "Unifesspa.UniPlus.Selecao.Application", "Validators", "ProcessosSeletivos");

        foreach (string arquivo in ArquivosDoGate)
        {
            yield return new[] { raizDomain, raizApplication, raizValidators }
                .Select(pasta => Path.Combine(pasta, arquivo))
                .FirstOrDefault(File.Exists) ?? Path.Combine(raizDomain, arquivo);
        }

        // A raiz cobre muitas dimensões além do cronograma: varrer o arquivo inteiro é deliberado,
        // porque uma comparação institucional em qualquer parte dele já seria uma violação.
        yield return Path.Combine(raizDomain, "ProcessoSeletivo.cs");
    }

    /// <summary>As regras extraídas da raiz moram em <c>Domain/ProcessosSeletivos</c>; a pasta inteira é varrida.</summary>
    private static IEnumerable<string> ArquivosDasRegrasDoProcesso()
    {
        string pasta = Caminho("src", "selecao", "Unifesspa.UniPlus.Selecao.Domain", "ProcessosSeletivos");
        return Directory.Exists(pasta) ? Directory.EnumerateFiles(pasta, "*.cs", SearchOption.AllDirectories) : [];
    }

    private static string Caminho(params string[] segmentos) =>
        Path.GetFullPath(Path.Combine([RaizDoRepo(), .. segmentos]));

    private static string RaizDoRepo([CallerFilePath] string origem = "") =>
        Path.GetFullPath(Path.Join(Path.GetDirectoryName(origem)!, "..", ".."));
}
