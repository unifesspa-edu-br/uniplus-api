namespace Unifesspa.UniPlus.Regras.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class ComponenteDeExibicaoCodigosTests
{
    [Fact]
    public void Codigos_ValoresDeclarados_ResolvemNosDoisSentidos()
    {
        (ComponenteDeExibicao Valor, string Codigo)[] pares =
        [
            (ComponenteDeExibicao.Moeda, "MOEDA"),
            (ComponenteDeExibicao.Situacao, "SITUACAO"),
        ];
        CatalogoDe<ComponenteDeExibicao>.Valores().Should().Equal(pares.Select(static par => par.Valor));
        foreach ((ComponenteDeExibicao valor, string codigo) in pares)
        {
            valor.Codigo().Should().Be(codigo);
            CatalogoDe<ComponenteDeExibicao>.TentarObter(codigo, out ComponenteDeExibicao obtido).Should().BeTrue();
            obtido.Should().Be(valor);
        }
    }
}
