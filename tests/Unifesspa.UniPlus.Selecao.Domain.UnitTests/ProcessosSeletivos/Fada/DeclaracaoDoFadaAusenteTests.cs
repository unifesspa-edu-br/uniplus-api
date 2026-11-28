namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Fada;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fada;

public sealed class DeclaracaoDoFadaAusenteTests
{
    [Fact(DisplayName = "A ausência da declaração é recusada como obrigatória no campo aplica")]
    public void RecusaDaAusencia_ApontaOCampoAplica()
    {
        FieldError recusa = DeclaracaoDoFada.RecusaDaAusencia();

        recusa.Field.Should().Be("aplica");
        recusa.Error.Code.Should().Be(CodigosDeValidacao.Obrigatorio);
    }
}
