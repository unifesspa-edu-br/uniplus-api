namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.AdulteracaoDeEnvelopeDeTeste;

public sealed class ChavesDasBancasNoEnvelopeTests
{
    [Fact]
    public void Reidratar_BancasDoDocumento_PreservaAsChaves()
    {
        byte[] bytes = VersoesDoEnvelopeDeTeste.BytesDoExemplo();
        Result<EnvelopeReidratado> resultado = VersoesDoEnvelopeDeTeste.ReidratarComoAbertura(bytes);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Configuracao.Cronograma.CronogramaFases.SelectMany(fase => fase.BancasRequeridas)
            .Select(banca => banca.Chave.Valor).Should().Contain("banca_fase_1");
        resultado.Value.Configuracao.Avaliacao.Etapas.SelectMany(etapa => etapa.Bancas)
            .Select(banca => banca.Chave.Valor).Should().Contain("banca_objetiva_1");
    }

    [Theory]
    [InlineData("etapas.0.bancas.0")]
    [InlineData("cronogramaFases.fases.1.bancasRequeridas.0")]
    public void Reidratar_BancaSemChave_Recusa(string caminho)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope => Navegar(envelope, caminho).Remove("chave"));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("chave");
    }

    [Theory]
    [InlineData("etapas.0.bancas.0")]
    [InlineData("cronogramaFases.fases.1.bancasRequeridas.0")]
    public void Reidratar_BancaComChaveMalformada_Recusa(string caminho)
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            Navegar(envelope, caminho)["chave"] = "Banca-invalida");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("chave");
    }

    [Fact]
    public void Reidratar_BancasDeFaseEEtapaComMesmaChave_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            Navegar(envelope, "etapas.0.bancas.0")["chave"] =
                Navegar(envelope, "cronogramaFases.fases.1.bancasRequeridas.0")["chave"]!.GetValue<string>());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain("chave");
    }
}
