namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Xunit;

public sealed class MensagemDoEnquadramentoTests
{
    [Fact]
    public void MensagemDoEnquadramento_CodigoDesconhecido_UsaMensagemDaLeitura()
    {
        LeituraDaRegraDeHomologacaoDaFase.MensagemDoEnquadramento(new DomainError("teste.desconhecido", "Mensagem interna."))
            .Should().Be("Confira a forma do enquadramento informado.");
    }

    [Fact]
    public void MensagemDoEnquadramento_FormaDoDocumentoInvalida_PreservaMensagem()
    {
        LeituraDaRegraDeHomologacaoDaFase.MensagemDoEnquadramento(new DomainError("PredicadoDnf.FormaJsonInvalida", "Forma inválida."))
            .Should().Be("Forma inválida.");
    }
}
