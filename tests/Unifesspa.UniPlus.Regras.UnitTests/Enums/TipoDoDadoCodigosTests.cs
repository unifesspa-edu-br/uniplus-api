namespace Unifesspa.UniPlus.Regras.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class TipoDoDadoCodigosTests
{
    [Fact(DisplayName = "Cada tipo do dado tem o código e o rótulo do contrato")]
    public void TipoDoDado_CadaValor_TemCodigoERotuloDeclarados() =>
        Enum.GetValues<TipoDoDado>()
            .ToDictionary(static valor => valor, static valor => (valor.Codigo(), valor.Rotulo()))
            .Should().Equal(new Dictionary<TipoDoDado, (string, string)>
            {
                [TipoDoDado.SimOuNao] = ("SIM_OU_NAO", "Sim ou não"),
                [TipoDoDado.Opcao] = ("OPCAO", "Opção"),
                [TipoDoDado.Numerico] = ("NUMERICO", "Número"),
                [TipoDoDado.Texto] = ("TEXTO", "Texto"),
                [TipoDoDado.Data] = ("DATA", "Data"),
                [TipoDoDado.Endereco] = ("ENDERECO", "Endereço"),
            });

    [Fact(DisplayName = "Os tipos do dado seguem a ordem de exibição e cada código volta ao seu valor")]
    public void TipoDoDado_Catalogo_SegueAOrdemDeDeclaracaoEResolveOCodigo()
    {
        CatalogoDe<TipoDoDado>.Valores().Should().Equal(
            TipoDoDado.SimOuNao, TipoDoDado.Opcao, TipoDoDado.Numerico, TipoDoDado.Texto, TipoDoDado.Data, TipoDoDado.Endereco);
        CatalogoDe<TipoDoDado>.TentarObter("OPCAO", out TipoDoDado lido).Should().BeTrue();
        lido.Should().Be(TipoDoDado.Opcao);
    }
}
