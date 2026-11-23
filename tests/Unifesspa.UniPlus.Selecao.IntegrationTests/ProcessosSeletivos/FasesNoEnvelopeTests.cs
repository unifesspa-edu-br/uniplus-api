namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Xunit;

/// <summary>
/// A leitura direta dos períodos da fase e da etapa no envelope congelado: o instante é lido na forma canônica
/// do envelope, o nó ausente ou nulo é a fase sem período e qualquer outra forma é falha, nunca "sem período".
/// </summary>
public sealed class FasesNoEnvelopeTests
{
    private static readonly DateTimeOffset Inicio = new DateTimeOffset(2040, 3, 10, 12, 0, 0, TimeSpan.Zero).AddTicks(1234560);
    private static readonly DateTimeOffset Fim = new DateTimeOffset(2040, 3, 20, 12, 0, 0, TimeSpan.Zero).AddTicks(9999990);

    [Fact(DisplayName = "O período da fase e da etapa é lido com os microssegundos que o envelope guarda")]
    public void Das_LeOPeriodoEmMicrossegundos()
    {
        string envelope = Envelope(
            faseInicio: Canonico(Inicio), faseFim: Canonico(Fim), etapaInicio: Canonico(Inicio), etapaFim: Canonico(Fim));

        FaseDaVersaoPublicada fase = FasesNoEnvelope.Das(envelope).Single();

        fase.Periodo.Inicio.Should().Be(Inicio);
        fase.Periodo.Fim.Should().Be(Fim);
        fase.Etapas.Single().Periodo.Inicio.Should().Be(Inicio);
        fase.Etapas.Single().Periodo.Fim.Should().Be(Fim);
    }

    [Fact(DisplayName = "O instante nulo ou ausente é a fase e a etapa sem período")]
    public void Das_InstanteNuloOuAusente_SemPeriodo()
    {
        JsonObject objeto = JsonNode.Parse(Envelope(null, null, null, null))!.AsObject();
        objeto["etapas"]![0]!.AsObject().Remove("fim");
        objeto["cronogramaFases"]!["fases"]![0]!.AsObject().Remove("fim");

        FaseDaVersaoPublicada fase = FasesNoEnvelope.Das(objeto.ToJsonString()).Single();

        fase.Periodo.Inicio.Should().BeNull();
        fase.Periodo.Fim.Should().BeNull();
        fase.Etapas.Single().Periodo.Inicio.Should().BeNull();
        fase.Etapas.Single().Periodo.Fim.Should().BeNull();
    }

    [Theory(DisplayName = "O instante fora da forma canônica do envelope no período da fase é falha, não ausência")]
    [InlineData("2020-01-01T00:00:00Z")]
    [InlineData("2020-01-01T00:00:00.123Z")]
    [InlineData("2020-01-01T00:00:00.1234567Z")]
    [InlineData("2020-01-01T00:00:00.123456+00:00")]
    [InlineData("não é um instante")]
    public void Das_InstanteDaFaseForaDaForma_Lanca(string instante)
    {
        string envelope = Envelope(faseInicio: instante, faseFim: null, etapaInicio: null, etapaFim: null);

        Action leitura = () => FasesNoEnvelope.Das(envelope);

        leitura.Should().Throw<PayloadForaDoPerfilCanonicoException>();
    }

    [Theory(DisplayName = "O instante fora da forma canônica do envelope no período da etapa é falha, não ausência")]
    [InlineData("2020-01-01T00:00:00Z")]
    [InlineData("2020-01-01T00:00:00.1234567Z")]
    public void Das_InstanteDaEtapaForaDaForma_Lanca(string instante)
    {
        string envelope = Envelope(faseInicio: null, faseFim: null, etapaInicio: null, etapaFim: instante);

        Action leitura = () => FasesNoEnvelope.Das(envelope);

        leitura.Should().Throw<PayloadForaDoPerfilCanonicoException>();
    }

    [Fact(DisplayName = "O instante que não é texto no envelope é falha, não ausência")]
    public void Das_InstanteQueNaoETexto_Lanca()
    {
        JsonObject objeto = JsonNode.Parse(Envelope(null, null, null, null))!.AsObject();
        objeto["etapas"]![0]!["inicio"] = 1_700_000_000;

        Action leitura = () => FasesNoEnvelope.Das(objeto.ToJsonString());

        leitura.Should().Throw<PayloadForaDoPerfilCanonicoException>();
    }

    private static string Canonico(DateTimeOffset instante) =>
        HashCanonicalComputer.SerializeInstantCanonical(instante);

    private static string Envelope(string? faseInicio, string? faseFim, string? etapaInicio, string? etapaFim) =>
        new JsonObject
        {
            ["etapas"] = new JsonArray(Etapa(etapaInicio, etapaFim)),
            ["cronogramaFases"] = new JsonObject { ["fases"] = new JsonArray(Fase(faseInicio, faseFim)) },
        }.ToJsonString();

    private static JsonObject Etapa(string? inicio, string? fim) =>
        new()
        {
            ["id"] = Guid.CreateVersion7(),
            ["nome"] = "Prova objetiva",
            ["faseCodigo"] = "AVALIACAO",
            ["inicio"] = inicio,
            ["fim"] = fim,
        };

    private static JsonObject Fase(string? inicio, string? fim) =>
        new()
        {
            ["id"] = Guid.CreateVersion7(),
            ["tipoFaseOrigemId"] = Guid.CreateVersion7(),
            ["codigo"] = "AVALIACAO",
            ["inicio"] = inicio,
            ["fim"] = fim,
            ["coletaInscricao"] = false,
        };
}
