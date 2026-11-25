namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

public sealed class TipoDeCampoNoModeloDaSelecaoTests
{
    private static ValueConverter Conversor() =>
        ModeloDaSelecao.Propriedade(typeof(FatoColetado), nameof(FatoColetado.TipoDeCampo)).GetValueConverter()
            ?? throw new InvalidOperationException("FatoColetado.TipoDeCampo não tem conversor.");

    [Fact(DisplayName = "A coluna do tipo de campo é texto de até 30 caracteres")]
    public void Coluna_DoTipoDeCampo_ETextoDeAte30Caracteres()
    {
        IProperty propriedade = ModeloDaSelecao.Propriedade(typeof(FatoColetado), nameof(FatoColetado.TipoDeCampo));

        propriedade.GetMaxLength().Should().Be(30);
        propriedade.GetColumnType().Should().Be("character varying(30)");
    }

    [Fact(DisplayName = "O tipo de campo vai para a coluna pelo código declarado")]
    public void Escrita_DoTipoDeCampo_EOCodigoDeclarado() =>
        Enum.GetValues<TipoDeCampo>().ToDictionary(static v => v, v => Conversor().ConvertToProvider(v))
            .Should().Equal(new Dictionary<TipoDeCampo, object?>
            {
                [TipoDeCampo.Numero] = "NUMERO",
                [TipoDeCampo.SimOuNao] = "SIM_OU_NAO",
                [TipoDeCampo.EscolhaUnica] = "ESCOLHA_UNICA",
                [TipoDeCampo.EscolhaMultipla] = "ESCOLHA_MULTIPLA",
                [TipoDeCampo.Texto] = "TEXTO",
                [TipoDeCampo.Data] = "DATA",
                [TipoDeCampo.Endereco] = "ENDERECO",
                [TipoDeCampo.Municipio] = "MUNICIPIO",
            });

    [Fact(DisplayName = "A leitura do código declarado devolve o tipo de campo")]
    public void Leitura_DoCodigoDeclarado_DevolveOTipo() =>
        Conversor().ConvertFromProvider("ESCOLHA_MULTIPLA").Should().Be(TipoDeCampo.EscolhaMultipla);

    [Theory(DisplayName = "O tipo de campo gravado que não é código declarado falha ao carregar, sem virar tipo nenhum")]
    [InlineData("NENHUMA")]
    [InlineData("selecao_unica")]
    [InlineData("0")]
    public void Leitura_DeTipoDesconhecido_Falha(string gravado) =>
        FluentActions.Invoking(() => Conversor().ConvertFromProvider(gravado)).Should().Throw<ArgumentException>();
}
