namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.DTOs;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

public sealed class CodigosDasPublicacoesPrevistasTests
{
    [Fact]
    public void SituacaoDoProdutoPrevisto_Valores_TemCodigoDeclarado() =>
        Enum.GetValues<SituacaoDoProdutoPrevisto>().Select(static valor => (valor, valor.Codigo()))
            .Should().Equal(
                (SituacaoDoProdutoPrevisto.Aberto, "ABERTO"),
                (SituacaoDoProdutoPrevisto.Publicado, "PUBLICADO"));

    [Fact]
    public void ImpedimentoDoProduto_Valores_TemCodigoDeclarado() =>
        Enum.GetValues<ImpedimentoDoProduto>().Select(static valor => (valor, valor.Codigo()))
            .Should().Equal(
                (ImpedimentoDoProduto.JaPublicado, "JA_PUBLICADO"),
                (ImpedimentoDoProduto.PreliminarNaoPublicado, "PRELIMINAR_NAO_PUBLICADO"),
                (ImpedimentoDoProduto.TipoDeAtoNaoVigente, "TIPO_DE_ATO_NAO_VIGENTE"));
}
