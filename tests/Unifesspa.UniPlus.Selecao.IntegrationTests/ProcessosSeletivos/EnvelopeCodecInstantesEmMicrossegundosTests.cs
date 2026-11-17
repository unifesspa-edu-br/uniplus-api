namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Globalization;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.AdulteracaoDeEnvelopeDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDoEnvelopeCodec;

/// <summary>
/// Os instantes do envelope têm seis casas fixas de fração: o período de inscrição, a janela da fase e a da etapa
/// são lidos sem perda dos microssegundos, e o decodificador recusa qualquer outra forma.
/// </summary>
public sealed class EnvelopeCodecInstantesEmMicrossegundosTests
{
    [Theory(DisplayName = "O instante com microssegundos no período e na janela da fase é lido sem perda")]
    [InlineData("periodo.inicio", "2026-03-02T00:00:00.000001Z")]
    [InlineData("periodo.fim", "2026-03-20T23:59:59.000001Z")]
    [InlineData("cronogramaFases.fases.0.inicio", "2026-03-02T00:00:00.000001Z")]
    [InlineData("cronogramaFases.fases.0.fim", "2026-03-20T23:59:59.000001Z")]
    public void Janela_ComMicrossegundos_ELidaSemPerda(string caminho, string instante)
    {
        EnvelopeReidratado envelope = ReidratarMutado(caminho, instante);

        DateTimeOffset esperado = DateTimeOffset.Parse(instante, CultureInfo.InvariantCulture);
        DateTimeOffset?[] lidos = caminho switch
        {
            "periodo.inicio" => [envelope.Dados.PeriodoInscricaoInicio],
            "periodo.fim" => [envelope.Dados.PeriodoInscricaoFim],
            "cronogramaFases.fases.0.inicio" => [envelope.Grafo.CronogramaFases[0].Inicio],
            _ => [envelope.Grafo.CronogramaFases[0].Fim],
        };
        lidos.Should().Equal(esperado);
    }

    [Theory(DisplayName = "O instante com microssegundos na janela da etapa é lido sem perda")]
    [InlineData("inicio", "2026-03-02T00:00:00.000001Z")]
    [InlineData("fim", "2026-03-20T23:59:59.999999Z")]
    public void JanelaDaEtapa_ComMicrossegundos_ELidaSemPerda(string chave, string instante)
    {
        EnvelopeReidratado envelope = ReidratarMutado(mutado => mutado["etapas"]!.AsArray()[0]![chave] = instante);

        EtapaProcesso etapa = envelope.Grafo.Etapas[0];
        (chave == "inicio" ? etapa.Inicio : etapa.Fim).Should().Be(DateTimeOffset.Parse(instante, CultureInfo.InvariantCulture));
    }

    [Theory(DisplayName = "O instante fora da forma de seis casas e Z é recusado com o caminho do campo")]
    [InlineData("periodo.inicio", "2026-03-02T00:00:00Z", "periodo.inicio")]
    [InlineData("periodo.fim", "2026-03-20T23:59:59.123Z", "periodo.fim")]
    [InlineData("cronogramaFases.fases.0.fim", "2026-03-20T23:59:59.1234567Z", "cronogramaFases.fases[0].fim")]
    [InlineData("cronogramaFases.fases.0.inicio", "2026-03-02T00:00:00.000000+00:00", "cronogramaFases.fases[0].inicio")]
    [InlineData("etapas.0.inicio", "2026-03-02T00:00:00Z", "etapas[0].inicio")]
    [InlineData("etapas.0.fim", "2026-03-20T23:59:59.1234567Z", "etapas[0].fim")]
    public void Instante_NaFormaAntigaOuDeOutroTamanho_Recusa(string caminho, string instante, string campoEsperado)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            ApoioDoEnvelopeCodecTipos.Definir(envelope, caminho, instante));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain($"'{campoEsperado}'");
    }
}
