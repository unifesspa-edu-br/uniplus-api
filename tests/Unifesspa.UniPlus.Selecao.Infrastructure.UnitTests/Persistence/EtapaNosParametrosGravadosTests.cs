namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

public sealed class EtapaNosParametrosGravadosTests
{
    [Fact(DisplayName = "Parâmetros com etapa preservam seu identificador ao serem gravados")]
    public void Parametros_ComEtapa_GravamEtapaIdEVoltamSemPerda()
    {
        Guid etapaId = Guid.NewGuid();
        Conferir(typeof(CriterioDesempate), new ParametrosDesempateMaiorNotaEtapa(etapaId), etapaId);
        Conferir(typeof(RegraEliminacao), new ParametrosEliminacaoPorNotaMinimaNaEtapa(etapaId, 5), etapaId);
        Conferir(typeof(RegraEliminacao), new ParametrosEliminacaoPorAusenciaNaEtapa(etapaId), etapaId);
    }

    [Theory(DisplayName = "Nota mínima ausente, inválida ou com caixa incorreta impede a leitura")]
    [InlineData("""{"$tipo":"notaMinimaEtapa","etapaId":"44444444-4444-4444-4444-444444444444"}""")]
    [InlineData("""{"$tipo":"notaMinimaEtapa","etapaId":"44444444-4444-4444-4444-444444444444","notaMinima":"abc"}""")]
    [InlineData("""{"$tipo":"notaMinimaEtapa","etapaId":"44444444-4444-4444-4444-444444444444","NotaMinima":5}""")]
    [InlineData("""{"$tipo":"notaMinimaEtapa","etapaId":"44444444-4444-4444-4444-444444444444","notaMinima":null}""")]
    [InlineData("""{"$tipo":"notaMinimaEtapa","EtapaId":"44444444-4444-4444-4444-444444444444","notaMinima":5}""")]
    [InlineData("""{"$tipo":"notaMinimaEtapa","etapaId":"44444444-4444-4444-4444-444444444444","notaMinima":5,"extra":1}""")]
    public void Eliminacao_NotaMinimaOuEtapaInvalida_RecusaLeitura(string json)
    {
        ValueConverter conversor = ModeloDaSelecao.Propriedade(typeof(RegraEliminacao), "Parametros").GetValueConverter()!;
        Action ler = () => conversor.ConvertFromProvider(json);
        ler.DeveRecusarComoDocumentoInvalido("ParametrosRegraEliminacao");
    }

    [Theory(DisplayName = "Variantes de desempate preservam os parâmetros gravados")]
    [InlineData("""{"$tipo":"maiorNotaEtapa","etapaId":"44444444-4444-4444-4444-444444444444"}""")]
    [InlineData("""{"$tipo":"maiorIdade"}""")]
    [InlineData("""{"$tipo":"idoso","idadeMinima":60}""")]
    [InlineData("""{"$tipo":"condicaoSobreDado","condicao":{"codigoDoDado":"IDADE","operador":"IGUAL","valor":60}}""")]
    [InlineData("""{"$tipo":"maiorNotaAreaEnem","areas":["REDACAO","MATEMATICA"]}""")]
    public void Desempate_VarianteGravada_PreservaParametros(string json) => ConferirJson(typeof(CriterioDesempate), json);

    [Theory(DisplayName = "Variantes de eliminação preservam os parâmetros gravados")]
    [InlineData("""{"$tipo":"notaMinimaEtapa","etapaId":"44444444-4444-4444-4444-444444444444","notaMinima":5}""")]
    [InlineData("""{"$tipo":"corteEmArea","areaCodigo":"REDACAO","minimo":400}""")]
    [InlineData("""{"$tipo":"zeroEmArea"}""")]
    [InlineData("""{"$tipo":"faltaEmDiaDeProvaEnem"}""")]
    [InlineData("""{"$tipo":"ausenciaNaEtapa","etapaId":"44444444-4444-4444-4444-444444444444"}""")]
    public void Eliminacao_VarianteGravada_PreservaParametros(string json) => ConferirJson(typeof(RegraEliminacao), json);

    [Theory(DisplayName = "Desempate recusa identificador com caixa incorreta ou chave desconhecida")]
    [InlineData("""{"$tipo":"maiorNotaEtapa","EtapaId":"44444444-4444-4444-4444-444444444444"}""")]
    [InlineData("""{"$tipo":"idoso","idadeMinima":60,"extra":1}""")]
    [InlineData("""{"$tipo":"maiorNotaAreaEnem","areas":null}""")]
    public void Desempate_ParametroInvalido_RecusaLeitura(string json)
    {
        ValueConverter conversor = ModeloDaSelecao.Propriedade(typeof(CriterioDesempate), "Parametros").GetValueConverter()!;
        Action ler = () => conversor.ConvertFromProvider(json);
        ler.DeveRecusarComoDocumentoInvalido("ParametrosCriterioDesempate");
    }

    private static void ConferirJson(Type entidade, string json)
    {
        ValueConverter conversor = ModeloDaSelecao.Propriedade(entidade, "Parametros").GetValueConverter()!;
        object parametros = conversor.ConvertFromProvider(json)!;
        string gravado = (string)conversor.ConvertToProvider(parametros)!;
        System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(gravado),
            System.Text.Json.Nodes.JsonNode.Parse(json)).Should().BeTrue();
    }

    private static void Conferir(Type entidade, object parametros, Guid etapaId)
    {
        ValueConverter conversor = ModeloDaSelecao.Propriedade(entidade, "Parametros").GetValueConverter()!;
        string gravado = (string)conversor.ConvertToProvider(parametros)!;
        using JsonDocument documento = JsonDocument.Parse(gravado);
        documento.RootElement.GetProperty("etapaId").GetGuid().Should().Be(etapaId);
        documento.RootElement.TryGetProperty("etapaRef", out _).Should().BeFalse();
        conversor.ConvertFromProvider(gravado).Should().Be(parametros);
    }
}
