namespace Unifesspa.UniPlus.Kernel.UnitTests.Results;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;

public sealed class FieldErrorTests
{
    private static readonly DomainError Erro = new("Item.Invalido", "O item é inválido.");

    [Fact(DisplayName = "A causa sem campo continua sem campo dentro do contêiner")]
    public void NoCaminhoDe_CausaSemCampo_ContinuaSemCampo() =>
        new FieldError(null, Erro).NoCaminhoDe("itens[0]").Field.Should().BeNull();

    [Fact(DisplayName = "A raiz relativa passa a ser o caminho do contêiner")]
    public void NoCaminhoDe_RaizRelativa_ViraOCaminhoDoConteiner() =>
        new FieldError(string.Empty, Erro).NoCaminhoDe("itens[0]").Field.Should().Be("itens[0]");

    [Fact(DisplayName = "O campo interno vira filho do contêiner")]
    public void NoCaminhoDe_CampoInterno_ViraFilhoDoConteiner() =>
        new FieldError("rotulo", Erro).NoCaminhoDe("itens[0]").Field.Should().Be("itens[0].rotulo");

    [Fact(DisplayName = "Os índices do campo são unidos ao caminho sem ponto")]
    public void NoCaminhoDe_CampoComIndices_JuntaSemPonto() =>
        new FieldError("[0][1].valor", Erro).NoCaminhoDe("exibicao").Field.Should().Be("exibicao[0][1].valor");

    [Theory(DisplayName = "O caminho vazio mantém a causa na raiz do documento")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("provaPresencial.municipios[0]")]
    [InlineData("[0].valor")]
    public void NoCaminhoDe_CaminhoVazio_MantemACausa(string? campo)
    {
        FieldError causa = new(campo, Erro, "FATO");

        causa.NoCaminhoDe(string.Empty).Should().BeSameAs(causa);
    }

    [Fact(DisplayName = "O código, a mensagem e a origem da causa seguem os mesmos")]
    public void NoCaminhoDe_MantemOErroEAOrigem()
    {
        FieldError doItem = new FieldError("rotulo", Erro, "FATO").NoCaminhoDe("itens[0]");

        doItem.Error.Should().Be(Erro);
        doItem.Origem.Should().Be("FATO");
    }

    [Fact(DisplayName = "O caminho do contêiner é obrigatório")]
    public void NoCaminhoDe_CaminhoNulo_Lanca() =>
        Assert.Throws<ArgumentNullException>(() => new FieldError("a", Erro).NoCaminhoDe(null!));
}
