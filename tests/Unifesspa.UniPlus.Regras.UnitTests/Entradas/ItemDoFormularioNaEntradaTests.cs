namespace Unifesspa.UniPlus.Regras.UnitTests.Entradas;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>O item do formulário na entrada: a apresentação e as regras do campo, de ida e de volta ao domínio.</summary>
public sealed class ItemDoFormularioNaEntradaTests
{
    private static readonly CondicaoPrecondicaoInput Condicao = new("ANTERIOR", "IGUAL", JsonSerializer.SerializeToElement(true));

    [Fact(DisplayName = "A apresentação do domínio vai à entrada com o tipo de campo no código canônico")]
    public void ParaEntrada_Apresentacao_LevaOCodigoDoTipo()
    {
        ApresentacaoDoCampoInput entrada = EntradaDeRegras.ParaEntrada(new ApresentacaoDoCampo("Nome", TipoDeCampo.Texto, "Como no documento.", pedirConfirmacao: true));

        entrada.Should().Be(new ApresentacaoDoCampoInput("Nome", "TEXTO", "Como no documento.", PedirConfirmacao: true));
    }

    [Fact(DisplayName = "A apresentação ausente na entrada fica sem texto: a recusa do rótulo é da forma do item")]
    public void Apresentacao_EntradaAusente_FicaSemTexto()
    {
        ApresentacaoDoCampo apresentacao = EntradaDeRegras.Apresentacao(null, TipoDeCampo.SimOuNao);

        apresentacao.Should().Be(new ApresentacaoDoCampo(string.Empty, TipoDeCampo.SimOuNao));
    }

    [Fact(DisplayName = "As regras do domínio vão à entrada com o token da obrigatoriedade e o predicado dela")]
    public void ParaEntrada_Regras_LevaTokenEPredicadoDaObrigatoriedade()
    {
        PredicadoDnf predicado = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar("ANTERIOR", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!;

        RegrasDoCampoInput entrada = EntradaDeRegras.ParaEntrada(new RegrasDoCampo(null, Obrigatoriedade.Quando(predicado), [new TamanhoTexto(1, 10)]));

        entrada.Exibicao.Should().BeNull();
        entrada.Obrigatoriedade!.Tipo.Should().Be("QUANDO");
        entrada.Obrigatoriedade.Predicado.Should().ContainSingle().Which.Should().ContainSingle().Which.Fato.Should().Be("ANTERIOR");
        entrada.Restricoes.Should().ContainSingle().Which.Tipo.Should().Be("TAMANHO_TEXTO");
        entrada.Impedimento.Should().BeNull();
    }

    [Fact(DisplayName = "Os predicados das regras reúnem a exibição, o da obrigatoriedade, o do impedimento e o de cada grupo de opções")]
    public void Predicados_TodasAsPartes_NaOrdem()
    {
        IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>> exibicao = [[Condicao]];
        IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>> obrigatoriedade = [[Condicao], [Condicao]];
        IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>> impedimento = [[Condicao, Condicao]];
        IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>> opcoes = [[Condicao]];
        RegrasDoCampoInput regras = new(
            exibicao,
            new ObrigatoriedadeInput("QUANDO", obrigatoriedade),
            [new RestricaoValorInput("OPCOES_PERMITIDAS", Entradas: [new OpcoesCondicionadasInput(opcoes, ["A"])])],
            new ImpedimentoInput(impedimento, "Não pode."));

        regras.Predicados().Should().Equal(exibicao, obrigatoriedade, impedimento, opcoes);
    }

    [Fact(DisplayName = "As regras sem nenhuma condição não trazem predicado, só os ausentes")]
    public void Predicados_SemCondicoes_SoAusentes() =>
        new RegrasDoCampoInput(null, new ObrigatoriedadeInput("SEMPRE")).Predicados().Should().OnlyContain(static p => p == null);
}
