namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using TestSupport;

/// <summary>
/// Guarda o arquivo de seed dos tipos de ato (<c>seeds/seed-tipos-ato.json</c>) contra
/// edições que passariam despercebidas: um código fora do formato, uma janela de
/// vigência incoerente, ou a quebra da invariante da ADR-0103.
/// </summary>
/// <remarks>
/// Estes testes leem o **arquivo real**. Um teste que construísse os dados em memória
/// e os verificasse contra si mesmo passaria por construção, e trocar um valor no JSON
/// não o quebraria — que é exatamente o acidente contra o qual o arquivo precisa de
/// proteção.
/// </remarks>
public sealed partial class SeedTiposAtoTests
{
    [GeneratedRegex(@"^[A-Z]+(_[A-Z]+)*$")]
    private static partial Regex FormatoCodigo();

    private const string CaminhoRelativo = "seeds/seed-tipos-ato.json";

    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web);

    [Fact(DisplayName = "O arquivo de seed existe e traz os 16 tipos de ato")]
    public void Seed_TrazOsDezesseisTipos()
    {
        LinhaSeed[] linhas = Carregar();

        linhas.Should().HaveCount(16);
        linhas.Select(l => l.Codigo).Should().OnlyHaveUniqueItems();
    }

    [Fact(DisplayName = "Todo código do seed respeita o formato que o agregado exige")]
    public void Seed_CodigosNoFormatoUpperSnake()
    {
        foreach (LinhaSeed linha in Carregar())
        {
            FormatoCodigo().IsMatch(linha.Codigo).Should().BeTrue(
                $"'{linha.Codigo}' precisa ser UPPER_SNAKE, senão o POST do seed volta 422");
        }
    }

    [Fact(DisplayName = "Todo tipo do seed nasce com vigência aberta")]
    public void Seed_VigenciaAberta()
    {
        foreach (LinhaSeed linha in Carregar())
        {
            linha.VigenciaInicio.Should().Be("2026-01-01", $"{linha.Codigo} deve compartilhar o início de vigência");
            linha.VigenciaFim.Should().BeNull($"{linha.Codigo} não pode nascer encerrado");
        }
    }

    [Fact(DisplayName = "Todo tipo do seed tem nome em pt-BR, não derivado do código")]
    public void Seed_NomesExplicitos()
    {
        foreach (LinhaSeed linha in Carregar())
        {
            linha.Nome.Should().NotBeNullOrWhiteSpace();
            linha.Nome.Should().NotBe(linha.Codigo, $"{linha.Codigo} precisa de um nome legível, não do próprio código");
            linha.Nome.Length.Should().BeGreaterThanOrEqualTo(2);
        }
    }

    [Fact(DisplayName = "ADR-0103: congela(retificador) == congela(retificado)")]
    public void Seed_RetificadorECongelanteComoORetificado()
    {
        Dictionary<string, LinhaSeed> porCodigo = Carregar().ToDictionary(l => l.Codigo, StringComparer.Ordinal);

        // Não é o rótulo do tipo que protege a integridade da configuração publicada
        // (RN08), é a classe de congelamento. Se um edital de abertura congela e a sua
        // retificação não, a retificação deixaria de produzir a nova versão congelada —
        // e nenhum teste de endpoint perceberia.
        porCodigo.Should().ContainKey("EDITAL_ABERTURA");
        porCodigo.Should().ContainKey("EDITAL_RETIFICACAO");

        porCodigo["EDITAL_RETIFICACAO"].CongelaConfiguracao
            .Should().Be(porCodigo["EDITAL_ABERTURA"].CongelaConfiguracao);

        porCodigo["EDITAL_ABERTURA"].CongelaConfiguracao.Should().BeTrue(
            "o edital de abertura é o ato que congela a configuração do certame");
    }

    [Fact(DisplayName = "Só os atos que encerram um resultado têm efeito irreversível")]
    public void Seed_EfeitoIrreversivelRestrito()
    {
        IReadOnlyList<string> irreversiveis =
            [.. Carregar().Where(l => l.EfeitoIrreversivel).Select(l => l.Codigo).Order(StringComparer.Ordinal)];

        irreversiveis.Should().BeEquivalentTo(["GABARITO_DEFINITIVO", "RESULTADO_FINAL"]);
    }

    [Fact(DisplayName = "Só os atos que o objeto admite uma vez são únicos por objeto")]
    public void Seed_UnicoPorObjetoRestrito()
    {
        IReadOnlyList<string> unicos =
            [.. Carregar().Where(l => l.UnicoPorObjeto).Select(l => l.Codigo).Order(StringComparer.Ordinal)];

        unicos.Should().BeEquivalentTo(["EDITAL_ABERTURA", "HOMOLOGACAO_INSCRICOES", "RESULTADO_FINAL"]);
    }

    private static LinhaSeed[] Carregar()
    {
        // Path.Combine descarta os segmentos anteriores quando um deles é enraizado.
        // O precedente é OpenApiEndpointTests.ResolveRepoPath, que valida o mesmo.
        Path.IsPathRooted(CaminhoRelativo).Should().BeFalse(
            "o caminho do seed é relativo à raiz do repositório");

        string caminho = Path.Combine(SolutionRootLocator.Locate(), CaminhoRelativo);

        File.Exists(caminho).Should().BeTrue($"o seed dos tipos de ato precisa existir em {CaminhoRelativo}");

        LinhaSeed[]? linhas = JsonSerializer.Deserialize<LinhaSeed[]>(File.ReadAllText(caminho), Opcoes);

        linhas.Should().NotBeNull();
        return linhas!;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instanciada por System.Text.Json ao desserializar o arquivo de seed.")]
    private sealed record LinhaSeed(
        string Codigo,
        string Nome,
        bool CongelaConfiguracao,
        bool UnicoPorObjeto,
        bool EfeitoIrreversivel,
        string VigenciaInicio,
        string? VigenciaFim,
        string? BaseLegal);
}
