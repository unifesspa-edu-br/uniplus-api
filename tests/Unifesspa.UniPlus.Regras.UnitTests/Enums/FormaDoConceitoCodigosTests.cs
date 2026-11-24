namespace Unifesspa.UniPlus.Regras.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class FormaDoConceitoCodigosTests
{
    [Fact(DisplayName = "Cada forma do conceito tem o código e o rótulo do contrato")]
    public void FormaDoConceito_CadaValor_TemCodigoERotuloDeclarados() =>
        Enum.GetValues<FormaDoConceito>()
            .ToDictionary(static valor => valor, static valor => (valor.Codigo(), valor.Rotulo()))
            .Should().Equal(new Dictionary<FormaDoConceito, (string, string)>
            {
                [FormaDoConceito.Numerico] = ("NUMERICO", "Numérico"),
                [FormaDoConceito.PorOpcoes] = ("POR_OPCOES", "Por opções"),
            });

    [Fact(DisplayName = "As formas do conceito seguem a ordem de exibição e o código desconhecido não é encontrado")]
    public void FormaDoConceito_Catalogo_SegueAOrdemDeDeclaracaoERecusaCodigoDesconhecido()
    {
        CatalogoDe<FormaDoConceito>.Valores().Should().Equal(FormaDoConceito.Numerico, FormaDoConceito.PorOpcoes);
        CatalogoDe<FormaDoConceito>.TentarObter("CATEGORICO", out _).Should().BeFalse();
        CatalogoDe<FormaDoConceito>.TentarObter("por_opcoes", out _).Should().BeFalse();
        CatalogoDe<FormaDoConceito>.TentarObter(null, out _).Should().BeFalse();
    }
}
