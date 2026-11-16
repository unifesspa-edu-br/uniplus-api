namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

using Xunit;

/// <summary>O vocabulário das condições que a regra de homologação sabe apurar: hoje só o resultado no retorno do INEP.</summary>
public sealed class TipoDeCondicaoDeHomologacaoTests
{
    [Fact(DisplayName = "O código do resultado no retorno do INEP é o publicado no documento e volta ao mesmo tipo")]
    public void Codigo_DoResultadoNoRetornoDoInep_IdaEVolta()
    {
        string codigo = TipoDeCondicaoDeHomologacao.ResultadoNoRetornoDoInep.Codigo();

        codigo.Should().Be("RESULTADO_NO_RETORNO_DO_INEP");
        TiposDeCondicaoDeHomologacao.DoCodigo(codigo).Value.Should().Be(TipoDeCondicaoDeHomologacao.ResultadoNoRetornoDoInep);
    }

    [Theory(DisplayName = "Condição que o sistema ainda não apura é recusada como não disponível, sem tentar adivinhar o tipo")]
    [InlineData("TAXA_PAGA_OU_ISENCAO_DEFERIDA")]
    [InlineData("DOCUMENTO_DEFERIDO")]
    [InlineData("resultado_no_retorno_do_inep")]
    [InlineData("")]
    [InlineData(null)]
    public void DoCodigo_CondicaoNaoApurada_ENaoDisponivel(string? codigo)
    {
        Result<TipoDeCondicaoDeHomologacao> resultado = TiposDeCondicaoDeHomologacao.DoCodigo(codigo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(HomologacaoInscricaoErrorCodes.CondicaoNaoDisponivel);
    }
}
