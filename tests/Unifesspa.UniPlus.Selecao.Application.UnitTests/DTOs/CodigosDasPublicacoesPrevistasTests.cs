namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.DTOs;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

public sealed class CodigosDasPublicacoesPrevistasTests
{
    [Fact]
    public void SituacaoDoProdutoPrevisto_Valores_TemCodigoERotuloDeclarados() =>
        Enum.GetValues<SituacaoDoProdutoPrevisto>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (SituacaoDoProdutoPrevisto.Aberto, "ABERTO", "Aberta"),
                (SituacaoDoProdutoPrevisto.Publicado, "PUBLICADO", "Publicada"));

    [Fact]
    public void ImpedimentoDoProduto_Valores_TemCodigoERotuloDeclarados() =>
        Enum.GetValues<ImpedimentoDoProduto>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (ImpedimentoDoProduto.JaPublicado, "JA_PUBLICADO", "Já publicada"),
                (ImpedimentoDoProduto.PreliminarNaoPublicado, "PRELIMINAR_NAO_PUBLICADO", "Preliminar não publicado"),
                (ImpedimentoDoProduto.TipoDeAtoNaoVigente, "TIPO_DE_ATO_NAO_VIGENTE", "Tipo de ato não vigente"));
}
