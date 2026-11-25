namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Queries.NotasDoEnem;
using Unifesspa.UniPlus.Selecao.Domain.Leiautes;

public sealed class FiltroDeTipoDosLeiautesDoInepTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Ler_SemSituacao_NaoFiltra(string? situacao)
    {
        Result<TipoDeLeiauteDoInep?> resultado = FiltroDeTipoDosLeiautesDoInep.Ler(situacao);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().BeNull();
    }

    [Fact]
    public void Ler_CodigoPublicado_Filtra()
    {
        Result<TipoDeLeiauteDoInep?> resultado = FiltroDeTipoDosLeiautesDoInep.Ler("ENVIO");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().Be(TipoDeLeiauteDoInep.Envio);
    }

    [Theory]
    [InlineData("EmBreve")]
    [InlineData("   ")]
    [InlineData(" ENVIO ")]
    [InlineData("em_breve")]
    [InlineData("inexistente")]
    public void Ler_CodigoDesconhecido_RecusaSemRepetirValor(string situacao)
    {
        Result<TipoDeLeiauteDoInep?> resultado = FiltroDeTipoDosLeiautesDoInep.Ler(situacao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FiltroDeTipoDosLeiautesDoInep.CodigoDeErro);
        resultado.Error.Message.Should().NotContain(situacao);
    }
}
