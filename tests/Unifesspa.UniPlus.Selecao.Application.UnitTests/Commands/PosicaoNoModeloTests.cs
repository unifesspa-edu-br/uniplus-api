namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

public sealed class PosicaoNoModeloTests
{
    private static readonly DomainError Erro = new("Modelo.Recusado", "O modelo foi recusado.");

    [Fact(DisplayName = "A recusa sem campo continua sem campo, e não aponta o modelo")]
    public void NoModelo_RecusaSemCampo_ContinuaSemCampo() =>
        PosicaoNoModelo.NoModelo(new FieldError(null, Erro)).Field.Should().BeNull();

    [Fact(DisplayName = "A raiz relativa da recusa passa a ser o modelo")]
    public void NoModelo_RaizRelativa_ViraOModelo() =>
        PosicaoNoModelo.NoModelo(new FieldError(string.Empty, Erro)).Field.Should().Be("modelo");

    [Fact(DisplayName = "O campo da recusa vira filho do modelo")]
    public void NoModelo_CampoInterno_ViraFilhoDoModelo() =>
        PosicaoNoModelo.NoModelo(new FieldError("itens[0].rotulo", Erro)).Field.Should().Be("modelo.itens[0].rotulo");

    [Fact(DisplayName = "A recusa sem campo não é movida para a posição de uma lista do modelo")]
    public void NoModelo_ComListaERecusaSemCampo_ContinuaSemCampo() =>
        PosicaoNoModelo.NoModelo(new FieldError(null, Erro), "itens", [3, 5]).Field.Should().BeNull();
}
