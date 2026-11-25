namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Xunit;

public sealed class MensagemDaFormaDoEnquadramentoTests
{
    [Fact]
    public void MensagemDaForma_CodigoDesconhecido_UsaMensagemDaGravacao()
    {
        MontagemDaRegraDeHomologacao.MensagemDaForma("CondicaoDnf.Desconhecida")
            .Should().Be("Não foi possível conferir a condição informada.");
    }
}
