namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

public sealed class AvaliadorFormularioTests
{
    private const string Etapa = "DADOS";

    [Theory]
    [InlineData(true, Ternario.Verdadeiro, EstadoFato.Indeterminado)]
    [InlineData(false, Ternario.Falso, EstadoFato.NaoInformado)]
    public void MesmoItem_EhObrigatorioParaUmPerfilEOpcionalParaOutro(
        bool concorreRenda, Ternario obrigatorioEsperado, EstadoFato estadoSemResposta)
    {
        DefinicaoFormulario formulario = Formulario(
            Item("CONCORRER_RENDA"),
            Item("RENDA_FAMILIAR", obrigatoriedade: Obrigatoriedade.Quando(Predicado(Condicao("CONCORRER_RENDA", Operador.Igual, true)))));

        AvaliacaoFormulario avaliacao = AvaliadorFormulario.Avaliar(
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

        AvaliacaoFormulario concluida = AvaliadorFormulario.Avaliar(formulario, Entrada(etapaConcluida: true));
        AvaliacaoFormulario emAberto = AvaliadorFormulario.Avaliar(formulario, Entrada(etapaConcluida: false));

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

        AvaliacaoFormulario avaliacao = AvaliadorFormulario.Avaliar(
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

        AvaliacaoFormulario avaliacao = AvaliadorFormulario.Avaliar(formulario, Entrada(etapaConcluida: true));

        avaliacao.Fatos["DERIVADO"].Estado.Should().Be(EstadoFato.Resolvido);
        avaliacao.Fatos["DERIVADO"].Valor!.Value.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void EtapaOculta_LevaSeusItensANaoAplicavel_MesmoComRespostaGravada()
    {
        DefinicaoFormulario formulario = new(
            [
                new DefinicaoEtapa(Etapa, exibicao: null, [Item("ESTRANGEIRO")]),
                new DefinicaoEtapa(
                    "DOCUMENTO_ESTRANGEIRO",
                    Predicado(Condicao("ESTRANGEIRO", Operador.Igual, true)),
                    [Item("PASSAPORTE")]),
            ],
            termos: [],
            derivacoes: []);

        AvaliacaoFormulario avaliacao = AvaliadorFormulario.Avaliar(
            formulario, Entrada(etapaConcluida: true, ("ESTRANGEIRO", false), ("PASSAPORTE", "AB123")));

        avaliacao.Itens[1].Visivel.Should().Be(Ternario.Falso);
        avaliacao.Fatos["PASSAPORTE"].Estado.Should().Be(EstadoFato.NaoAplicavel);
    }

    [Fact]
    public void Derivado_EhResolvidoEntreOsItens_AntesDoItemQueOCita()
    {
        DefinicaoFormulario formulario = new(
            [
                new DefinicaoEtapa(Etapa, exibicao: null, [Item("PCD")]),
                new DefinicaoEtapa(
                    "LAUDO",
                    exibicao: null,
                    [Item("LAUDO_MEDICO", exibicao: Predicado(Condicao("MODALIDADE", Operador.Em, new[] { "AC_PCD" })))]),
            ],
            termos: [],
            [Derivacao("MODALIDADE", Predicado(Condicao("PCD", Operador.Igual, true)), "AC_PCD")]);

        AvaliacaoFormulario avaliacao = AvaliadorFormulario.Avaliar(formulario, Entrada(etapaConcluida: false, ("PCD", true)));

        avaliacao.Itens[1].Visivel.Should().Be(Ternario.Verdadeiro);
        avaliacao.Itens.Select(static i => i.FatoCodigo).Should().Equal("PCD", "LAUDO_MEDICO");
    }

    [Theory]
    [InlineData("\"MED\"", EstadoFato.Resolvido, 0)]
    [InlineData("\"ENF\"", EstadoFato.NaoInformado, 1)]
    public void OpcaoFormadaPelasRespostas_RespostaForaDasOpcoesNaoVale(
        string listaDeEspera, EstadoFato estadoEsperado, int violacoes)
    {
        DefinicaoFormulario formulario = Formulario(
            Item("CURSOS"),
            Item("LISTA_ESPERA", obrigatoriedade: Obrigatoriedade.Nunca, restricoes: [new OpcoesDasRespostas(["CURSOS"])]));

        AvaliacaoFormulario avaliacao = AvaliadorFormulario.Avaliar(
            formulario,
            Entrada(
                etapaConcluida: true,
                ("CURSOS", new[] { "MED" }),
                ("LISTA_ESPERA", JsonDocument.Parse(listaDeEspera).RootElement)));

        avaliacao.Fatos["LISTA_ESPERA"].Estado.Should().Be(estadoEsperado);
        avaliacao.Itens[1].RestricoesVioladas.Should().HaveCount(violacoes);
    }

    [Fact]
    public void OpcoesFiltradasPorRespostaAnterior_RecusamOpcaoDeOutroFiltro()
    {
        OpcoesPermitidas municipios = new(
        [
            new OpcoesCondicionadas(Predicado(Condicao("UF", Operador.Igual, "PA")), ["MARABA", "BELEM"]),
            new OpcoesCondicionadas(Predicado(Condicao("UF", Operador.Igual, "AM")), ["MANAUS"]),
        ]);
        DefinicaoFormulario formulario = Formulario(Item("UF"), Item("MUNICIPIO", restricoes: [municipios]));

        AvaliacaoFormulario avaliacao = AvaliadorFormulario.Avaliar(
            formulario, Entrada(etapaConcluida: true, ("UF", "PA"), ("MUNICIPIO", "MANAUS")));

        avaliacao.Itens[1].RestricoesVioladas.Should().ContainSingle();
        avaliacao.Fatos["MUNICIPIO"].Estado.Should().Be(EstadoFato.Indeterminado, "o campo é obrigatório e a resposta não vale");
    }

    [Theory]
    [InlineData("\"BRASIL\"", EstadoFato.Resolvido)]
    [InlineData("\"MARABA\"", EstadoFato.Indeterminado)]
    public void OpcoesComGrupoAindaIndeterminado_SoPendemPelaOpcaoQueDependeDele(string municipio, EstadoFato esperado)
    {
        OpcoesPermitidas opcoes = new(
        [
            new OpcoesCondicionadas(quando: null, ["BRASIL"]),
            new OpcoesCondicionadas(Predicado(Condicao("UF", Operador.Igual, "PA")), ["MARABA"]),
        ]);
        DefinicaoFormulario formulario = Formulario(
            Item("UF", obrigatoriedade: Obrigatoriedade.Nunca),
            Item("LOCAL", restricoes: [opcoes]));

        AvaliacaoFormulario avaliacao = AvaliadorFormulario.Avaliar(
            formulario, Entrada(etapaConcluida: false, ("LOCAL", JsonDocument.Parse(municipio).RootElement)));

        avaliacao.Fatos["LOCAL"].Estado.Should().Be(esperado);
        avaliacao.Itens[1].RestricoesVioladas.Should().BeEmpty();
    }

    [Fact]
    public void ItemNuncaObrigatorio_NaoFicaComObrigatoriedadePendente_EnquantoAExibicaoEhIndeterminada()
    {
        DefinicaoFormulario formulario = Formulario(
            Item("ESTRANGEIRO"),
            Item("PASSAPORTE", exibicao: Predicado(Condicao("ESTRANGEIRO", Operador.Igual, true)), obrigatoriedade: Obrigatoriedade.Nunca));

        AvaliacaoItem passaporte = AvaliadorFormulario.Avaliar(formulario, Entrada(etapaConcluida: false)).Itens[1];

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

        AvaliacaoTermo termo = AvaliadorFormulario.Avaliar(
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

        AvaliacaoFormulario avaliacao = AvaliadorFormulario.Avaliar(formulario, Entrada(etapaConcluida: true));

        avaliacao.Fatos["A"].Estado.Should().Be(EstadoFato.Indeterminado);
        avaliacao.Fatos["B"].Estado.Should().Be(EstadoFato.Indeterminado);
    }

    private static DefinicaoFormulario Formulario(params DefinicaoItem[] itens) =>
        new([new DefinicaoEtapa(Etapa, exibicao: null, itens)], termos: [], derivacoes: []);

    private static DefinicaoItem Item(
        string fato,
        PredicadoDnf? exibicao = null,
        Obrigatoriedade? obrigatoriedade = null,
        IReadOnlyList<RestricaoValor>? restricoes = null) =>
        new(fato, exibicao, obrigatoriedade ?? Obrigatoriedade.Sempre, restricoes ?? []);

    private static EntradaAvaliacaoFormulario Entrada(bool etapaConcluida, params (string Fato, object Valor)[] respostas) =>
        new(
            respostas.ToDictionary(
                static r => r.Fato,
                static r => r.Valor is JsonElement json ? json : JsonSerializer.SerializeToElement(r.Valor),
                StringComparer.Ordinal),
            etapaConcluida ? new HashSet<string>([Etapa], StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal),
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal));

    private static CondicaoDnf Condicao(string fato, Operador operador, object valor) =>
        CondicaoDnf.Criar(
            fato,
            operador,
            valor is JsonElement json ? json : JsonSerializer.SerializeToElement(valor)).Value!;

    private static PredicadoDnf Predicado(params CondicaoDnf[] condicoes) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([.. condicoes.Select(static c => (0, c))]).Value!;

    private static RegrasDerivacaoFato Derivacao(string fato, PredicadoDnf quando, string contribui)
    {
        RegraDerivacao regra = RegraDerivacao.Criar(quando, contribui).Value!;
        return RegrasDerivacaoFato.Criar(fato, [regra], regra.FatosCitados, [contribui]).Value!;
    }
}
