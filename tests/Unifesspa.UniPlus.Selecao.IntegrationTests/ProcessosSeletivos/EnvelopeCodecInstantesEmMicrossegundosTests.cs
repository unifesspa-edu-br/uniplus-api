namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.AdulteracaoDeEnvelopeDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDoEnvelopeCodec;

/// <summary>
/// Os instantes do envelope têm seis casas fixas de fração: o período de inscrição, a janela da fase e a da etapa
/// fazem a ida e volta byte a byte com os microssegundos, e o decodificador recusa qualquer outra forma.
/// </summary>
public sealed class EnvelopeCodecInstantesEmMicrossegundosTests
{
    [Theory(DisplayName = "O instante com microssegundos no período e na janela da fase volta byte a byte")]
    [InlineData("periodo.inicio", "2026-03-02T00:00:00.000001Z")]
    [InlineData("periodo.fim", "2026-03-20T23:59:59.000001Z")]
    [InlineData("cronogramaFases.fases.0.inicio", "2026-03-02T00:00:00.000001Z")]
    [InlineData("cronogramaFases.fases.0.fim", "2026-03-20T23:59:59.000001Z")]
    public void Janela_ComMicrossegundos_VoltaByteAByte(string caminho, string instante)
    {
        (byte[] originais, byte[] mutados, byte[] recodificados) = MutarEReidratar(caminho, instante);

        recodificados.Should().NotEqual(originais);
        recodificados.Should().Equal(mutados);
    }

    [Theory(DisplayName = "O instante com microssegundos na janela da etapa volta byte a byte")]
    [InlineData("inicio", "2026-03-02T00:00:00.000001Z")]
    [InlineData("fim", "2026-03-20T23:59:59.999999Z")]
    public void JanelaDaEtapa_ComMicrossegundos_VoltaByteAByte(string chave, string instante)
    {
        (byte[] originais, byte[] mutados, byte[] recodificados) = MutarEReidratar(
            envelope => envelope["etapas"]!.AsArray()[0]![chave] = instante);

        recodificados.Should().NotEqual(originais);
        recodificados.Should().Equal(mutados);
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
