namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;
using AwesomeAssertions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Xunit;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.AdulteracaoDeEnvelopeDeTeste;

public sealed class EnvelopeCodecRecusasDosPrazosTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reidratar_PrazoESuspensividadeInvalidos_Acumula(bool etapa)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            JsonObject parametros = Parametros(envelope, etapa);
            parametros["prazoValor"] = "0.0000";
            parametros["suspensividadePrimeiraInstanciaValor"] = "1.0000";
            parametros["suspensividadePrimeiraInstanciaUnidade"] = null;
        });

        string entidade = etapa ? "RecursoDaEtapa" : "RegraRecursoFase";
        resultado.Errors.Select(static erro => erro.Error.Code).Should().Equal(
            $"{entidade}.PrazoNaoPositivo", $"{entidade}.SuspensividadeIncompleta");
    }

    private static JsonObject Parametros(JsonObject envelope, bool etapa) => etapa
        ? envelope["etapas"]!.AsArray().Select(e => e!.AsObject())
            .SelectMany(e => e["recursos"]!.AsArray().Select(r => r!.AsObject()))
            .First(r => r["ancora"]!.GetValue<string>() == "AtoPublicado")["parametros"]!.AsObject()
        : envelope["cronogramaFases"]!["fases"]!.AsArray().Select(f => f!.AsObject())
            .Single(f => f["regraRecurso"] is JsonObject)["regraRecurso"]!["parametros"]!.AsObject();
}
