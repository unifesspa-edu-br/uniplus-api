namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// Com regras mantidas antes das novas, a recusa volta à posição na lista que o pedido trouxe.
/// </summary>
public sealed class PosicaoDasRegrasNovasTests
{
    private static FieldError Recusa(string? campo) => new(campo, new DomainError("codigo", "mensagem"));

    [Theory(DisplayName = "A recusa de uma regra nova volta à posição dela na lista do pedido")]
    [InlineData("regrasDeComprovacao[2].documento.momento.faseId", "regrasDeComprovacao[0].documento.momento.faseId")]
    [InlineData("regrasDeComprovacao[4].itens[1].documento.momento.etapaId", "regrasDeComprovacao[2].itens[1].documento.momento.etapaId")]
    public void SemAsMantidas_RegraNova_VoltaAPosicaoDoPedido(string campo, string esperado)
    {
        PosicaoDasRegrasNovas.SemAsMantidas(Recusa(campo), 2).Field.Should().Be(esperado);
    }

    [Theory(DisplayName = "A recusa de uma regra mantida perde o campo, e a que não tem posição segue como veio")]
    [InlineData("regrasDeComprovacao[0].documento.momento.faseId", null)]
    [InlineData("regrasDeComprovacao[1].itens[0].documento.momento.faseId", null)]
    [InlineData("formularioCodigo", "formularioCodigo")]
    [InlineData(null, null)]
    public void SemAsMantidas_RegraMantidaOuSemPosicao_PerdeOuMantemOCampo(string? campo, string? esperado)
    {
        PosicaoDasRegrasNovas.SemAsMantidas(Recusa(campo), 2).Field.Should().Be(esperado);
    }
}
