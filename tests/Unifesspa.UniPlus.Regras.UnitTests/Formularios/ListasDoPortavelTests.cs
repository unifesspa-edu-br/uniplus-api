namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Leituras;

using static Unifesspa.UniPlus.Regras.UnitTests.Formularios.PortavelDeTeste;

/// <summary>
/// Como a conversão do formulário portável lê as listas: todos os elementos, a posição de cada recusa na lista enviada, a
/// lista nula como lista vazia e a composição do campo da recusa dentro do elemento.
/// </summary>
public sealed class ListasDoPortavelTests
{
    private static readonly string FaixaNumerica = TipoRestricaoValor.FaixaNumerica.Codigo();
    private static readonly string OpcoesPermitidas = TipoRestricaoValor.OpcoesPermitidas.Codigo();

    private static RestricaoValorDto Faixa(decimal? minimo, decimal? maximo) => new(FaixaNumerica, minimo, maximo, null, null, null, null);

    private static RestricaoValorDto TipoDesconhecido() => new("TIPO_FICTICIO", null, null, null, null, null, null);

    private static Result<DefinicaoFormulario> ComRestricoes(params RestricaoValorDto[] restricoes)
    {
        ItemPortavel item = Item("RENDA");
        return Formulario(item with { Regras = item.Regras with { Restricoes = restricoes } }).ParaDefinicao();
    }

    [Fact(DisplayName = "A recusa de cada elemento da lista traz a posição dele na lista enviada, e os válidos entre eles não mudam a posição")]
    public void ParaDefinicao_RestricoesRecusadasEntreValidas_MantemAPosicaoDeCada()
    {
        Result<DefinicaoFormulario> definicao = ComRestricoes(Faixa(1, 2), TipoDesconhecido(), Faixa(1, 2), TipoDesconhecido());

        definicao.Errors.Select(static e => e.Field).Should().Equal(
            "paginas[0].itens[0].regras.restricoes[1]", "paginas[0].itens[0].regras.restricoes[3]");
    }

    [Fact(DisplayName = "Todos os elementos da lista são lidos, mesmo depois de uma recusa")]
    public void ParaDefinicao_TresRestricoesRecusadas_RecusaAsTres()
    {
        ComRestricoes(TipoDesconhecido(), TipoDesconhecido(), TipoDesconhecido()).Errors.Should().HaveCount(3);
    }

    [Fact(DisplayName = "Elementos nulos contam na posição e só a recusa de forma volta")]
    public void ParaDefinicao_ItensNulos_RecusaAPosicaoDeCadaUm()
    {
        FormularioPortavel formulario = Formulario(null!, Item("RENDA"), null!, Item(" "));

        formulario.ParaDefinicao().Errors.Select(static e => e.Field).Should().Equal("paginas[0].itens[0]", "paginas[0].itens[2]");
    }

    [Fact(DisplayName = "A lista nula vale como lista vazia")]
    public void ParaDefinicao_ListasNulas_ValemComoListasVazias()
    {
        ItemPortavel item = Item("RENDA");
        FormularioPortavel formulario = Formulario(item with { Regras = item.Regras with { Restricoes = null! } });

        formulario.ParaDefinicao().IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "O campo da recusa de uma condição dentro de um grupo de opções soma o caminho de cada elemento que a contém")]
    public void ParaDefinicao_CondicaoInvalidaNoGrupoDeOpcoes_SomaOCaminhoDeCadaElemento()
    {
        JsonElement valor = JsonSerializer.SerializeToElement(true);
        RestricaoValorDto opcoes = new(
            OpcoesPermitidas, null, null, [new OpcoesCondicionadasDto([[new("DADO", "INVALIDO", valor)]], ["A"])], null, null, null);

        ComRestricoes(opcoes).Errors.Select(static e => e.Field).Should().Equal(
            "paginas[0].itens[0].regras.restricoes[0].entradas[0].quando[0][0].operador");
    }
}
