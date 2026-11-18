namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Seed;

/// <summary>
/// Prova <b>positivamente</b> o que as invariantes de <c>RECURSO-PRAZO-ANCORADO-EM-ATO</c>
/// declaram sobre a contagem do prazo de recurso (UNI-REQ-0113/UNI-REQ-0112/UNI-REQ-0116).
/// </summary>
/// <remarks>
/// <para>
/// A regra é dado versionado, e o hash prova apenas que o texto não mudou depois — não que
/// ele diz a coisa certa. Apagar a frase que descrevia a política revogada e regenerar o
/// hash deixaria o rol silencioso sobre a política vigente, e nada quebraria. Por isso cada
/// asserção aqui exige a <b>presença</b> de uma afirmação, não a ausência da antiga.
/// </para>
/// <para>
/// As invariantes são texto de negócio, lido por quem audita o certame — por isso as
/// asserções procuram os termos que o edital usa, e não uma sintaxe que o rol não tem.
/// </para>
/// </remarks>
public sealed class PrazoInterposicaoInvariantesSemanticaTests
{
    private const string CodigoDaRegra = "RECURSO-PRAZO-ANCORADO-EM-ATO";

    private static IReadOnlyList<string> Invariantes()
    {
        RegraCatalogoSeedItem regra = RegraCatalogoSeed.Itens.Single(
            i => string.Equals(i.Codigo, CodigoDaRegra, StringComparison.Ordinal));

        using JsonDocument documento = JsonDocument.Parse(regra.InvariantesJson);
        return [.. documento.RootElement.EnumerateArray().Select(e => e.GetString()!)];
    }

    /// <summary>
    /// Localiza a invariante que trata de um assunto, para que a falha aponte a frase
    /// ausente em vez de "nenhum item da lista bate".
    /// </summary>
    private static string InvarianteQueFala(string termo, string assunto)
    {
        string? encontrada = Invariantes().SingleOrDefault(
            i => i.Contains(termo, StringComparison.OrdinalIgnoreCase));

        encontrada.Should().NotBeNull(
            $"o rol de regras precisa declarar, como dado versionado, {assunto} — sem isso, a política vigente não está em lugar nenhum que o auditor do certame consiga ler");
        return encontrada!;
    }

    [Fact(DisplayName = "As invariantes declaram as DUAS unidades válidas da interposição: dias úteis em valor inteiro, e horas")]
    public void Invariantes_DeclaramAsUnidadesValidasDaInterposicao()
    {
        string invariante = InvarianteQueFala("interposição corre exclusivamente em dia útil", "que a interposição corre sobre dia útil");

        invariante.Should().Contain("dias úteis",
            "a unidade principal precisa estar declarada por extenso");
        invariante.Should().Contain("inteiro", "dias úteis só é declarável em valor inteiro");
        invariante.Should().Contain("horas", "horas é a segunda unidade declarável, para prazo menor que um dia");
    }

    [Fact(DisplayName = "As invariantes declaram a recusa de dia corrido e a de fração como causas distintas")]
    public void Invariantes_DeclaramAsDuasRecusas()
    {
        string invariante = InvarianteQueFala("interposição corre exclusivamente em dia útil", "as recusas do prazo de interposição");

        invariante.Should().Contain("dias corridos são recusados",
            "dia corrido encolheria a janela sempre que calhasse de cair em feriado");
        invariante.Should().Contain("fração de dia útil",
            "fração não tem leitura unívoca e é recusada por conta própria");
        invariante.Should().Contain("causa própria",
            "as duas recusas têm remediações diferentes, e o edital precisa saber que são erros distintos");
    }

    [Fact(DisplayName = "As invariantes declaram que a contagem sobre dia útil depende de calendário E de convenção, nas duas contagens que exigem")]
    public void Invariantes_DeclaramADependenciaDeCalendarioEConvencao()
    {
        string invariante = InvarianteQueFala("contagem sobre dia útil depende", "de que a contagem sobre dia útil depende de calendário e de convenção");

        invariante.Should().Contain("calendário de dias úteis vigente",
            "sem calendário não há como saber quais dias são úteis");
        invariante.Should().Contain("convenção de contagem",
            "sem convenção declarada, duas leituras legítimas fecham a janela em instantes diferentes");
        invariante.Should().Contain("nas duas unidades",
            "a interposição em horas também depende — só as horas situadas em dia útil avançam o relógio");
        invariante.Should().Contain("suspensividade em dias úteis",
            "a suspensividade em dias úteis é a outra contagem que depende");
    }

    [Fact(DisplayName = "As invariantes declaram que a suspensividade em horas ou dias corridos NÃO depende de calendário nem de convenção")]
    public void Invariantes_DeclaramAIndependenciaDaSuspensividadeCorrida()
    {
        string invariante = InvarianteQueFala("suspensividade em horas ou em dias corridos", "que a suspensividade corrida é independente");

        invariante.Should().Contain("não depende de calendário nem de convenção de contagem",
            "declarar a independência evita que o gate seja lido como universal");
        invariante.Should().Contain("sem distinguir úteis de não úteis",
            "é essa a razão da independência, e ela precisa estar escrita, não subentendida");
    }

    [Fact(DisplayName = "As invariantes descrevem a publicação que inicia o prazo em linguagem clara")]
    public void Invariantes_DescrevemPublicacaoQueIniciaOPrazo()
    {
        string texto = string.Join(' ', Invariantes());
        texto.Should().NotContain("âncora").And.NotContain("janela").And.NotContain("append-only");
        texto.Should().Contain("publicação de um produto preliminar da própria fase");
        texto.Should().Contain("julgamento e retificação são novos fatos");
    }

    [Fact(DisplayName = "As invariantes explicam a ausência de prazo de suspensividade")]
    public void Invariantes_SemPrazoDeSuspensividade_DeclaramConsequencia()
    {
        string invariante = InvarianteQueFala("sem prazo de suspensividade", "a ausência de prazo de suspensividade");
        invariante.Should().Contain("a pendência não impede atos irreversíveis");
        invariante.Should().NotContain("null");
    }

    [Fact(DisplayName = "O documento admite horas e dias úteis no prazo de interposição")]
    public void EsquemaDoDocumento_DeclaraAsUnidadesDaInterposicao()
    {
        JsonNode unidade = ArquivosDoEsquema.DoAssembly().PorNome["comuns.schema.json"]
            ["$defs"]!["propriedadesDoRecurso"]!["properties"]!["prazoUnidade"]!;
        unidade["enum"]!.AsArray().Select(item => item!.GetValue<string>())
            .Should().BeEquivalentTo("horas", "diasUteis");
    }

    [Fact(DisplayName = "Nenhuma invariante ainda afirma a política revogada de que dias úteis é recusado na interposição")]
    public void Invariantes_NaoAfirmamMaisAPoliticaRevogada()
    {
        // Contraprova das positivas acima: sozinha ela seria satisfeita por apagar a frase,
        // que é exatamente o que a issue proíbe tratar como suficiente. Junto delas, prova
        // que a substituição trocou a afirmação em vez de acumular as duas, contraditórias.
        Invariantes().Should().NotContain(
            i => i.Contains("DIAS_UTEIS é recusado na INTERPOSIÇÃO", StringComparison.OrdinalIgnoreCase),
            "a política revogada não pode coexistir com a vigente no mesmo dado versionado");
    }
}
