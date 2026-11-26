namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Xunit;

public sealed class RecusaDoTipoDeEtapaTests
{
    [Fact(DisplayName = "O tipo de etapa que o cadastro não tem é recusado com o código do processo seletivo e o texto cita como o tipo foi citado")]
    public void NaoEncontradoOuInativo_CitaOTipoComoFoiCitado()
    {
        DomainError erro = RecusaDoTipoDeEtapa.NaoEncontradoOuInativo("PROVA_ORAL");

        erro.Code.Should().Be("ProcessoSeletivo.TipoEtapaNaoEncontradoOuInativo");
        erro.Message.Should().Be("Tipo de etapa PROVA_ORAL não encontrado ou não está ativo.");
    }
}
