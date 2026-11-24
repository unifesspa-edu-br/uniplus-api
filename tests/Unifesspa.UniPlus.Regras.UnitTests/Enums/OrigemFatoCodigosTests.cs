namespace Unifesspa.UniPlus.Regras.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class OrigemFatoCodigosTests
{
    [Fact(DisplayName = "Cada origem do dado tem o código e o rótulo do contrato")]
    public void OrigemFato_CadaValor_TemCodigoERotuloDeclarados() =>
        Enum.GetValues<OrigemFato>()
            .ToDictionary(static valor => valor, static valor => (valor.Codigo(), valor.Rotulo()))
            .Should().Equal(new Dictionary<OrigemFato, (string, string)>
            {
                [OrigemFato.Calculado] = ("CALCULADO", "Calculado"),
                [OrigemFato.Declarado] = ("DECLARADO", "Declarado"),
                [OrigemFato.Integracao] = ("INTEGRACAO", "Integração"),
            });

    [Fact(DisplayName = "As origens do dado seguem a ordem de exibição e o código antigo ou desconhecido não é encontrado")]
    public void OrigemFato_Catalogo_SegueAOrdemDeDeclaracaoERecusaCodigoDesconhecido()
    {
        CatalogoDe<OrigemFato>.Valores().Should().Equal(OrigemFato.Calculado, OrigemFato.Declarado, OrigemFato.Integracao);
        CatalogoDe<OrigemFato>.TentarObter("DERIVADO", out _).Should().BeFalse();
        CatalogoDe<OrigemFato>.TentarObter("declarado", out _).Should().BeFalse();
        CatalogoDe<OrigemFato>.TentarObter(null, out _).Should().BeFalse();
    }
}
