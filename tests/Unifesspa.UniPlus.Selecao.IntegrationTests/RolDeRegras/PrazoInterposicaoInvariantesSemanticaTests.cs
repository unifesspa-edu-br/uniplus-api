namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Seed;

/// <summary>
/// Prova <b>positivamente</b> o que as invariantes de <c>RECURSO_PRAZO_CONTADO_DA_PUBLICACAO_DO_ATO</c>
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
    private const string CodigoDaRegra = "RECURSO_PRAZO_CONTADO_DA_PUBLICACAO_DO_ATO";

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

    [Fact(DisplayName = "As invariantes declaram as duas unidades válidas dos prazos: dias úteis em valor inteiro, e horas")]
    public void Invariantes_DeclaramAsUnidadesValidasDosPrazos()
    {
        string invariante = InvarianteQueFala("os prazos correm exclusivamente em dia útil", "que os prazos correm sobre dia útil");

        invariante.Should().Contain("dias úteis",
            "a unidade principal precisa estar declarada por extenso");
        invariante.Should().Contain("inteiro", "dias úteis só é declarável em valor inteiro");
        invariante.Should().Contain("horas", "horas é a segunda unidade declarável, para prazo menor que um dia");
    }

    [Fact(DisplayName = "As invariantes declaram a recusa de dia corrido e a de fração como causas distintas")]
    public void Invariantes_DeclaramAsDuasRecusas()
    {
        string invariante = InvarianteQueFala("os prazos correm exclusivamente em dia útil", "as recusas do prazo");

        invariante.Should().Contain("dias corridos são recusados",
            "dia corrido encolheria o prazo sempre que calhasse de cair em feriado");
        invariante.Should().Contain("fração de dia útil",
            "fração não tem leitura unívoca e é recusada por conta própria");
        invariante.Should().Contain("causa própria",
            "as duas recusas têm remediações diferentes, e o edital precisa saber que são erros distintos");
    }

    [Fact(DisplayName = "As invariantes declaram que a contagem sobre dia útil depende de calendário E de convenção, em todo prazo de recurso")]
    public void Invariantes_DeclaramADependenciaDeCalendarioEConvencao()
    {
        string invariante = InvarianteQueFala("contagem sobre dia útil depende", "de que a contagem sobre dia útil depende de calendário e de convenção");

        invariante.Should().Contain("calendário de dias úteis vigente",
            "sem calendário não há como saber quais dias são úteis");
        invariante.Should().Contain("convenção de contagem",
            "sem convenção declarada, duas leituras legítimas fecham o prazo em instantes diferentes");
        invariante.Should().Contain("nas duas unidades",
            "o prazo em horas também depende — só as horas situadas em dia útil avançam o relógio");
        invariante.Should().Contain("nas duas instâncias",
            "o prazo da 2ª instância é contado como o da 1ª");
    }

    [Fact(DisplayName = "As invariantes declaram o efeito suspensivo obrigatório da 1ª instância e o efeito sempre presente da 2ª")]
    public void Invariantes_DeclaramOEfeitoSuspensivo()
    {
        string invariante = InvarianteQueFala("efeito suspensivo do recurso na 1ª instância", "o efeito suspensivo escolhido pelo edital");

        invariante.Should().Contain("a escolha é obrigatória", "a falta da declaração é recusada, e não lida como ausência de efeito");
        InvarianteQueFala("2ª instância sempre tem efeito suspensivo", "que a 2ª instância sempre suspende")
            .Should().Contain("sem configuração");
    }

    [Fact(DisplayName = "As invariantes declaram a guarda da vaga como declaração separada do efeito suspensivo e a recusa de não guardar com instância superior")]
    public void Invariantes_DeclaramAGuardaDaVaga()
    {
        InvarianteQueFala("guarda da vaga em disputa", "a guarda da vaga")
            .Should().Contain("sem declaração, vale guardar até o resultado definitivo");
        InvarianteQueFala("não pode declarar que a vaga não fica guardada", "a recusa de não guardar com instância superior")
            .Should().Contain("com instância superior");
    }

    [Fact(DisplayName = "As invariantes declaram o prazo próprio da 2ª instância: declarado com instância superior e recusado sem ela")]
    public void Invariantes_DeclaramOPrazoDaSegundaInstancia()
    {
        string invariante = InvarianteQueFala("o prazo da 2ª instância é próprio", "o prazo próprio da 2ª instância");

        invariante.Should().Contain("é recusado quando não admite");
        invariante.Should().Contain("publicação do indeferimento ao candidato");
    }

    [Fact(DisplayName = "As invariantes descrevem a publicação que inicia o prazo em linguagem clara")]
    public void Invariantes_DescrevemPublicacaoQueIniciaOPrazo()
    {
        string texto = string.Join(' ', Invariantes());
        texto.Should().NotContain("âncora").And.NotContain("janela").And.NotContain("append-only");
        texto.Should().Contain("publicação de uma publicação prevista preliminar da própria fase");
        texto.Should().Contain("julgamento e retificação são novos fatos");
    }

    [Fact(DisplayName = "O documento admite horas, dias úteis e dias nos prazos, para o domínio recusar dias corridos com causa própria")]
    public void EsquemaDoDocumento_DeclaraAsUnidadesDosPrazos()
    {
        JsonNode propriedades = ArquivosDoEsquema.DoAssembly().PorNome["comuns.schema.json"]
            ["$defs"]!["propriedadesDoRecurso"]!["properties"]!;

        foreach (string campo in new[] { "prazoUnidade", "prazoSegundaInstanciaUnidade" })
        {
            propriedades[campo]!["enum"]!.AsArray().Select(item => item!.GetValue<string>())
                .Should().BeEquivalentTo("HORAS", "DIAS", "DIAS_UTEIS");
        }
    }

    [Fact(DisplayName = "Nenhuma invariante afirma a política revogada: dias úteis recusado na interposição ou período de suspensividade com prazo")]
    public void Invariantes_NaoAfirmamMaisAPoliticaRevogada()
    {
        // Contraprova das positivas acima: sozinha ela seria satisfeita por apagar a frase.
        // Junto delas, prova que a substituição trocou a afirmação em vez de acumular as duas, contraditórias.
        Invariantes().Should().NotContain(
            i => i.Contains("DIAS_UTEIS é recusado na INTERPOSIÇÃO", StringComparison.OrdinalIgnoreCase)
                || i.Contains("suspensividade", StringComparison.OrdinalIgnoreCase),
            "a política revogada não pode coexistir com a vigente no mesmo dado versionado");
    }
}
