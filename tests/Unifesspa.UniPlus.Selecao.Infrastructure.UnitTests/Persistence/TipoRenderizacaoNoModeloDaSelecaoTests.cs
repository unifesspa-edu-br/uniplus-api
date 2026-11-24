namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

public sealed class TipoRenderizacaoNoModeloDaSelecaoTests
{
    private static ValueConverter Conversor() =>
        ModeloDaSelecao.Propriedade(typeof(FatoColetado), nameof(FatoColetado.TipoRenderizacao)).GetValueConverter()
            ?? throw new InvalidOperationException("FatoColetado.TipoRenderizacao não tem conversor.");

    [Fact(DisplayName = "A coluna do tipo de campo é texto de até 30 caracteres")]
    public void Coluna_DoTipoDeCampo_ETextoDeAte30Caracteres()
    {
        IProperty propriedade = ModeloDaSelecao.Propriedade(typeof(FatoColetado), nameof(FatoColetado.TipoRenderizacao));

        propriedade.GetMaxLength().Should().Be(30);
        propriedade.GetColumnType().Should().Be("character varying(30)");
    }

    [Fact(DisplayName = "O tipo de campo vai para a coluna pelo código declarado")]
    public void Escrita_DoTipoDeCampo_EOCodigoDeclarado() =>
        Enum.GetValues<TipoRenderizacao>().ToDictionary(static v => v, v => Conversor().ConvertToProvider(v))
            .Should().Equal(new Dictionary<TipoRenderizacao, object?>
            {
                [TipoRenderizacao.Numero] = "NUMERO",
                [TipoRenderizacao.SimOuNao] = "SIM_OU_NAO",
                [TipoRenderizacao.EscolhaUnica] = "ESCOLHA_UNICA",
                [TipoRenderizacao.EscolhaMultipla] = "ESCOLHA_MULTIPLA",
                [TipoRenderizacao.Texto] = "TEXTO",
                [TipoRenderizacao.Data] = "DATA",
                [TipoRenderizacao.Endereco] = "ENDERECO",
                [TipoRenderizacao.Municipio] = "MUNICIPIO",
            });

    [Fact(DisplayName = "A leitura do código declarado devolve o tipo de campo")]
    public void Leitura_DoCodigoDeclarado_DevolveOTipo() =>
        Conversor().ConvertFromProvider("ESCOLHA_MULTIPLA").Should().Be(TipoRenderizacao.EscolhaMultipla);

    [Theory(DisplayName = "O tipo de campo gravado que não é código declarado falha ao carregar, sem virar tipo nenhum")]
    [InlineData("NENHUMA")]
    [InlineData("selecao_unica")]
    [InlineData("0")]
    public void Leitura_DeTipoDesconhecido_Falha(string gravado) =>
        FluentActions.Invoking(() => Conversor().ConvertFromProvider(gravado)).Should().Throw<ArgumentException>();
}
