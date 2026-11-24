namespace Unifesspa.UniPlus.Regras.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class QuantidadeDeValoresCodigosTests
{
    [Fact(DisplayName = "Cada quantidade de valores tem o código e o rótulo do contrato")]
    public void QuantidadeDeValores_CadaValor_TemCodigoERotuloDeclarados() =>
        Enum.GetValues<QuantidadeDeValores>()
            .ToDictionary(static valor => valor, static valor => (valor.Codigo(), valor.Rotulo()))
            .Should().Equal(new Dictionary<QuantidadeDeValores, (string, string)>
            {
                [QuantidadeDeValores.ValorUnico] = ("VALOR_UNICO", "Um valor"),
                [QuantidadeDeValores.VariosValores] = ("VARIOS_VALORES", "Vários valores"),
            });

    [Fact(DisplayName = "As quantidades de valores seguem a ordem de exibição e o código desconhecido não é encontrado")]
    public void QuantidadeDeValores_Catalogo_SegueAOrdemDeDeclaracaoERecusaCodigoDesconhecido()
    {
        CatalogoDe<QuantidadeDeValores>.Valores().Should().Equal(QuantidadeDeValores.ValorUnico, QuantidadeDeValores.VariosValores);
        CatalogoDe<QuantidadeDeValores>.TentarObter("valor_unico", out _).Should().BeFalse();
        CatalogoDe<QuantidadeDeValores>.TentarObter(null, out _).Should().BeFalse();
    }
}
