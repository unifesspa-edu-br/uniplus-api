namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

public sealed class FiltroDeSituacaoDosCertamesPublicadosTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Ler_SemSituacao_NaoFiltra(string? situacao)
    {
        Result<SituacaoDoCertame?> resultado = FiltroDeSituacaoDosCertamesPublicados.Ler(situacao);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().BeNull();
    }

    [Fact]
    public void Ler_CodigoPublicado_Filtra()
    {
        Result<SituacaoDoCertame?> resultado = FiltroDeSituacaoDosCertamesPublicados.Ler("EM_BREVE");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().Be(SituacaoDoCertame.EmBreve);
    }

    [Theory]
    [InlineData("EmBreve")]
    [InlineData("   ")]
    [InlineData(" EM_BREVE ")]
    [InlineData("em_breve")]
    [InlineData("inexistente")]
    public void Ler_CodigoDesconhecido_RecusaSemRepetirValor(string situacao)
    {
        Result<SituacaoDoCertame?> resultado = FiltroDeSituacaoDosCertamesPublicados.Ler(situacao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FiltroDeSituacaoDosCertamesPublicados.CodigoDeErro);
        resultado.Error.Message.Should().NotContain(situacao);
    }
}
