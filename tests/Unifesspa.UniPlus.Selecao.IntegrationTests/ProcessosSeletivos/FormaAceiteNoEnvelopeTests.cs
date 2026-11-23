namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.AdulteracaoDeEnvelopeDeTeste;

public sealed class FormaAceiteNoEnvelopeTests
{
    [Fact(DisplayName = "Forma de aceite desconhecida é recusada como conteúdo malformado")]
    public void Reidratar_FormaAceiteDesconhecida_Recusa()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
            envelope["formularios"]![0]!["termos"]![0]!["formaAceite"] = "DESCONHECIDA");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }

    [Fact(DisplayName = "O conteúdo congelado com formas de aceite mantém os bytes")]
    public void Codificar_TermosComFormaAceite_MantemBytes()
    {
        byte[] esperado = File.ReadAllBytes(Path.Join(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "ProcessosSeletivos", "Fixtures", "envelope-rico.json"));
        byte[] atual = CorpusEnvelope.Canonicalizador.Canonicalizar(CorpusEnvelopeEntrada.Entrada(CorpusEnvelopeProcessoRico.ProcessoRico())).Bytes;

        atual.Should().Equal(esperado);
    }
}
