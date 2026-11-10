namespace Unifesspa.UniPlus.Authorization.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Abstractions;

public sealed class CatalogoDesatualizadoExceptionTests
{
    [Fact(DisplayName = "A exceção sem argumentos traz a mensagem padrão em português")]
    public void SemArgumentos_TrazMensagemPadrao() =>
        new CatalogoDesatualizadoException().Message.Should().Contain("catálogo");

    [Fact(DisplayName = "A exceção preserva a mensagem e a causa informadas")]
    public void ComMensagemECausa_Preserva()
    {
        InvalidOperationException causa = new("causa");

        CatalogoDesatualizadoException excecao = new("mensagem", causa);

        excecao.Message.Should().Be("mensagem");
        excecao.InnerException.Should().BeSameAs(causa);
        new CatalogoDesatualizadoException("só mensagem").Message.Should().Be("só mensagem");
    }
}
