namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Conferencias;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Todo tipo de regra que o catálogo pode conter precisa ser consultável pela API. O filtro
/// da listagem trafega o código canônico de wire; um tipo declarado no enum sem código, ou com
/// código que não volta ao mesmo tipo, seria um conjunto de regras que existe no banco e que
/// nenhum cliente consegue listar.
/// </summary>
public sealed class TipoRegraConsultavelTests
{
    private static IReadOnlyList<TipoRegra> TiposReais() =>
        [.. Enum.GetValues<TipoRegra>()];

    [Fact(DisplayName = "Todo tipo de regra, exceto a sentinela, tem código de wire e volta ao mesmo tipo na ida e volta")]
    public void TodoTipoTemCodigoConsultavel()
    {
        TiposReais().Should().NotBeEmpty();

        TiposReais().Should().AllSatisfy(tipo =>
        {
            string codigo = tipo.Codigo();
            codigo.Should().NotBeNullOrWhiteSpace();
            CatalogoDe<TipoRegra>.Obter(codigo).Should().Be(tipo,
                "o filtro `tipo` da listagem converte o texto recebido de volta pelo mesmo mapa — "
                + "uma ida e volta que não fecha faria a API recusar o código que ela própria devolve");
        });
    }

    [Fact(DisplayName = "Nenhum código de wire se repete entre tipos — o filtro precisa resolver um tipo só")]
    public void CodigosDeWireSaoUnicos()
    {
        TiposReais().Select(static t => t.Codigo()).Should().OnlyHaveUniqueItems();
    }

}
