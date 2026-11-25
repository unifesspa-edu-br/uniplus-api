namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Queries.RolDeRegras;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class FiltroDeTipoDasRegrasDoCatalogoTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Ler_SemSituacao_NaoFiltra(string? situacao)
    {
        Result<TipoRegra?> resultado = FiltroDeTipoDasRegrasDoCatalogo.Ler(situacao);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().BeNull();
    }

    [Fact]
    public void Ler_CodigoPublicado_Filtra()
    {
        Result<TipoRegra?> resultado = FiltroDeTipoDasRegrasDoCatalogo.Ler("REGRA_CALCULO");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().Be(TipoRegra.RegraCalculo);
    }

    [Theory]
    [InlineData("EmBreve")]
    [InlineData("   ")]
    [InlineData(" REGRA_CALCULO ")]
    [InlineData("em_breve")]
    [InlineData("inexistente")]
    public void Ler_CodigoDesconhecido_RecusaSemRepetirValor(string situacao)
    {
        Result<TipoRegra?> resultado = FiltroDeTipoDasRegrasDoCatalogo.Ler(situacao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FiltroDeTipoDasRegrasDoCatalogo.CodigoDeErro);
        resultado.Error.Message.Should().NotContain(situacao);
    }
}
