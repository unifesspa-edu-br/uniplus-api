namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

using static ApoioDaAvaliacaoDoFormulario;

public class AvaliadorFormularioTests : TestesDeAvaliacao
{
    [Theory]
    [InlineData(true, Ternario.Verdadeiro, EstadoFato.Indeterminado)]
    [InlineData(false, Ternario.Falso, EstadoFato.NaoInformado)]
    public void MesmoItem_EhObrigatorioParaUmPerfilEOpcionalParaOutro(
        bool concorreRenda, Ternario obrigatorioEsperado, EstadoFato estadoSemResposta)
    {
        DefinicaoFormulario formulario = Formulario(
            Item("CONCORRER_RENDA"),
            Item("RENDA_FAMILIAR", obrigatoriedade: Obrigatoriedade.Quando(Predicado(Condicao("CONCORRER_RENDA", Operador.Igual, true)))));

        AvaliacaoFormulario avaliacao = AvaliarDefinicao(
            formulario, Entrada(etapaConcluida: true, ("CONCORRER_RENDA", concorreRenda)));

        avaliacao.Itens[1].Obrigatorio.Should().Be(obrigatorioEsperado);
        avaliacao.Fatos["RENDA_FAMILIAR"].Estado.Should().Be(estadoSemResposta);
    }

    [Theory]
    [InlineData(Operador.Diferente, "\"X\"")]
    [InlineData(Operador.NaoEm, "[\"X\"]")]
    public void OpcionalEmBranco_ComEtapaConcluida_TornaFalsaInclusiveANegacao(Operador operador, string valor)
    {
        DefinicaoFormulario formulario = Formulario(
            Item("APELIDO", obrigatoriedade: Obrigatoriedade.Nunca),
            Item("DETALHE", exibicao: Predicado(Condicao("APELIDO", operador, JsonDocument.Parse(valor).RootElement))));

        AvaliacaoFormulario concluida = AvaliarDefinicao(formulario, Entrada(etapaConcluida: true));
        AvaliacaoFormulario emAberto = AvaliarDefinicao(formulario, Entrada(etapaConcluida: false));

        concluida.Fatos["APELIDO"].Estado.Should().Be(EstadoFato.NaoInformado);
        concluida.Itens[1].Visivel.Should().Be(Ternario.Falso, "o opcional em branco não satisfaz a negação por omissão");
        concluida.Fatos["DETALHE"].Estado.Should().Be(EstadoFato.NaoAplicavel, "a regra seguinte não trava");
        emAberto.Fatos["APELIDO"].Estado.Should().Be(EstadoFato.Indeterminado, "o não informado só nasce ao concluir a etapa");
        emAberto.Fatos["DETALHE"].Estado.Should().Be(EstadoFato.Indeterminado);
    }

    [Theory]
    [InlineData("\"   \"")]
    [InlineData("[]")]
    public void TextoEmBrancoOuListaVazia_ContamComoSemResposta(string resposta)
    {
        DefinicaoFormulario formulario = Formulario(Item("APELIDO", obrigatoriedade: Obrigatoriedade.Nunca));

        AvaliacaoFormulario avaliacao = AvaliarDefinicao(
            formulario, Entrada(etapaConcluida: true, ("APELIDO", JsonDocument.Parse(resposta).RootElement)));

        avaliacao.Fatos["APELIDO"].Estado.Should().Be(EstadoFato.NaoInformado);
    }

    [Fact]
    public void Derivacao_AceitaDependenciaNaoInformada()
    {
        DefinicaoFormulario formulario = new(
            [new DefinicaoEtapa(Etapa, exibicao: null, [Item("OPCAO", obrigatoriedade: Obrigatoriedade.Nunca)])],
            termos: [],
            [Derivacao("DERIVADO", Predicado(Condicao("OPCAO", Operador.Igual, "A")), "X")]);

        AvaliacaoFormulario avaliacao = AvaliarDefinicao(formulario, Entrada(etapaConcluida: true));

        avaliacao.Fatos["DERIVADO"].Estado.Should().Be(EstadoFato.Resolvido);
        avaliacao.Fatos["DERIVADO"].Valor!.Value.GetArrayLength().Should().Be(0);
    }

    private static DefinicaoFormulario FormularioComEtapaDoDocumentoEstrangeiro() => new(
        [
            new DefinicaoEtapa(Etapa, exibicao: null, [Item("ESTRANGEIRO")]),
            new DefinicaoEtapa("DOCUMENTO_ESTRANGEIRO", Predicado(Condicao("ESTRANGEIRO", Operador.Igual, true)), [Item("PASSAPORTE")]),
        ],
        termos: [],
        derivacoes: []);

    [Fact]
    public void EtapaOculta_LevaSeusItensANaoAplicavel_MesmoComRespostaGravada()
    {
        AvaliacaoFormulario avaliacao = AvaliarDefinicao(
            FormularioComEtapaDoDocumentoEstrangeiro(), Entrada(etapaConcluida: true, ("ESTRANGEIRO", false), ("PASSAPORTE", "AB123")));

        avaliacao.Itens[1].Visivel.Should().Be(Ternario.Falso);
        avaliacao.Fatos["PASSAPORTE"].Estado.Should().Be(EstadoFato.NaoAplicavel);
    }

    private static DefinicaoFormulario FormularioComModalidadeDerivadaDePcd() => new(
        [
            new DefinicaoEtapa(Etapa, exibicao: null, [Item("PCD")]),
            new DefinicaoEtapa(
                "LAUDO",
                exibicao: null,
                [Item("LAUDO_MEDICO", exibicao: Predicado(Condicao("MODALIDADE", Operador.Em, new[] { "AC_PCD" })))]),
        ],
        termos: [],
        [Derivacao("MODALIDADE", Predicado(Condicao("PCD", Operador.Igual, true)), "AC_PCD")]);

    [Fact]
    public void Derivado_EhResolvidoEntreOsItens_AntesDoItemQueOCita()
    {
        AvaliacaoFormulario avaliacao = AvaliarDefinicao(FormularioComModalidadeDerivadaDePcd(), Entrada(etapaConcluida: false, ("PCD", true)));

        avaliacao.Itens[1].Visivel.Should().Be(Ternario.Verdadeiro);
        avaliacao.Itens.Select(static i => i.FatoCodigo).Should().Equal("PCD", "LAUDO_MEDICO");
    }

    [Fact(DisplayName = "A avaliação diz de cada seção se ela aparece")]
    public void Etapas_DizemSeAparecem()
    {
        DefinicaoFormulario formulario = new(
            [
                new DefinicaoEtapa("DADOS", exibicao: null, [Item("TIPO_ENDERECO")]),
                new DefinicaoEtapa("ALDEIA", Predicado(Condicao("TIPO_ENDERECO", Operador.Igual, "ALDEIA")), [Item("NOME_ALDEIA")]),
            ],
            termos: [],
            derivacoes: []);

        AvaliacaoFormulario avaliacao = AvaliarDefinicao(formulario, Entrada(etapaConcluida: false, ("TIPO_ENDERECO", "URBANO")));

        avaliacao.Etapas.Select(static e => (e.Codigo, e.Visivel)).Should().Equal(("DADOS", Ternario.Verdadeiro), ("ALDEIA", Ternario.Falso));
    }

    [Fact]
    public void ItemNuncaObrigatorio_NaoFicaComObrigatoriedadePendente_EnquantoAExibicaoEhIndeterminada()
    {
        DefinicaoFormulario formulario = Formulario(
            Item("ESTRANGEIRO"),
            Item("PASSAPORTE", exibicao: Predicado(Condicao("ESTRANGEIRO", Operador.Igual, true)), obrigatoriedade: Obrigatoriedade.Nunca));

        AvaliacaoItem passaporte = AvaliarDefinicao(formulario, Entrada(etapaConcluida: false)).Itens[1];

        passaporte.Visivel.Should().Be(Ternario.Indeterminado);
        passaporte.Obrigatorio.Should().Be(Ternario.Falso);
    }

    [Theory]
    [InlineData(true, Ternario.Verdadeiro)]
    [InlineData(false, Ternario.Falso)]
    public void TermoCondicionado_AparecePara_QuemSatisfazACondicao(bool concorreRenda, Ternario esperado)
    {
        DefinicaoFormulario formulario = new(
            [new DefinicaoEtapa(Etapa, exibicao: null, [Item("CONCORRER_RENDA")])],
            [new DefinicaoTermo("CONSULTA_BANCO_CENTRAL", Predicado(Condicao("CONCORRER_RENDA", Operador.Igual, true)), Obrigatoriedade.Sempre)],
            derivacoes: []);

        AvaliacaoTermo termo = AvaliarDefinicao(
            formulario, Entrada(etapaConcluida: true, ("CONCORRER_RENDA", concorreRenda))).Termos.Single();

        termo.Visivel.Should().Be(esperado);
        termo.Obrigatorio.Should().Be(esperado);
    }

    [Fact]
    public void DerivadosEmCiclo_FicamIndeterminados()
    {
        DefinicaoFormulario formulario = new(
            [new DefinicaoEtapa(Etapa, exibicao: null, [])],
            termos: [],
            [
                Derivacao("A", Predicado(Condicao("B", Operador.Em, new[] { "X" })), "X"),
                Derivacao("B", Predicado(Condicao("A", Operador.Em, new[] { "X" })), "X"),
            ]);

        AvaliacaoFormulario avaliacao = AvaliarDefinicao(formulario, Entrada(etapaConcluida: true));

        avaliacao.Fatos["A"].Estado.Should().Be(EstadoFato.Indeterminado);
        avaliacao.Fatos["B"].Estado.Should().Be(EstadoFato.Indeterminado);
    }
}
