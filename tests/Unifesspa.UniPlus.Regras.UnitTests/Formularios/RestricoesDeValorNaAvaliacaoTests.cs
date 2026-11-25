namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

using static ApoioDaAvaliacaoDoFormulario;

public class RestricoesDeValorNaAvaliacaoTests : TestesDeAvaliacao
{
    [Theory]
    [InlineData("\"MED\"", EstadoFato.Resolvido, 0)]
    [InlineData("\"ENF\"", EstadoFato.NaoInformado, 1)]
    public void OpcaoFormadaPelasRespostas_RespostaForaDasOpcoesNaoVale(
        string listaDeEspera, EstadoFato estadoEsperado, int violacoes)
    {
        DefinicaoFormulario formulario = Formulario(
            Item("CURSOS"),
            Item("LISTA_ESPERA", obrigatoriedade: Obrigatoriedade.Nunca, restricoes: [new OpcoesDasRespostas(["CURSOS"])]));

        AvaliacaoFormulario avaliacao = AvaliarDefinicao(
            formulario,
            Entrada(
                paginaConcluida: true,
                ("CURSOS", new[] { "MED" }),
                ("LISTA_ESPERA", JsonDocument.Parse(listaDeEspera).RootElement)));

        avaliacao.Fatos["LISTA_ESPERA"].Estado.Should().Be(estadoEsperado);
        avaliacao.Itens[1].RestricoesVioladas.Should().HaveCount(violacoes);
    }

    [Theory]
    [InlineData("PA", "1504208", EstadoFato.Resolvido, 0)]
    [InlineData("MA", "1504208", EstadoFato.NaoInformado, 1)]
    [InlineData(null, "1504208", EstadoFato.NaoInformado, 1)]
    [InlineData("PA", "9904208", EstadoFato.NaoInformado, 1)]
    public void MunicipioDaUf_ValeSoComMunicipioDaUfRespondida(string? uf, string municipio, EstadoFato estadoEsperado, int violacoes)
    {
        DefinicaoFormulario formulario = Formulario(
            Item("NATURALIDADE_UF", obrigatoriedade: Obrigatoriedade.Nunca),
            Item("NATURALIDADE_MUNICIPIO", obrigatoriedade: Obrigatoriedade.Nunca, restricoes: [new MunicipiosDaUf("NATURALIDADE_UF")]));
        (string, object)[] respostas = uf is null
            ? [("NATURALIDADE_MUNICIPIO", municipio)]
            : [("NATURALIDADE_UF", uf), ("NATURALIDADE_MUNICIPIO", municipio)];

        AvaliacaoFormulario avaliacao = AvaliarDefinicao(formulario, Entrada(paginaConcluida: true, respostas));

        avaliacao.Fatos["NATURALIDADE_MUNICIPIO"].Estado.Should().Be(estadoEsperado);
        avaliacao.Itens[1].RestricoesVioladas.Should().HaveCount(violacoes);
    }

    [Theory(DisplayName = "A resposta fora do formato do campo de texto viola a restrição de formato; a válida passa")]
    [InlineData("CPF", "111.111.111-11", false)]
    [InlineData("CPF", "529.982.247-25", true)]
    [InlineData("EMAIL", "sem-arroba", false)]
    [InlineData("EMAIL", "candidato@unifesspa.edu.br", true)]
    [InlineData("LIVRE", "qualquer texto", true)]
    public void CampoDeTexto_RespostaForaDoFormato_ViolaORestricaoDeFormato(string formato, string resposta, bool atende)
    {
        DefinicaoFormulario formulario = Formulario(
            new DefinicaoItem("CONTATO", null, Obrigatoriedade.Sempre, [], formato: formato));

        AvaliacaoFormulario avaliacao = AvaliarDefinicao(formulario, Entrada(paginaConcluida: true, ("CONTATO", resposta)));

        avaliacao.Fatos["CONTATO"].Estado.Should().Be(atende ? EstadoFato.Resolvido : EstadoFato.Indeterminado);
        avaliacao.Itens[0].RestricoesVioladas.Select(static r => r.Tipo).Should().Equal(
            atende ? [] : [TipoRestricaoValor.FormatoTexto]);
    }

    [Theory(DisplayName = "O NOME de um formulário congelado em NOME_PESSOA não é conferido pelo formato; o nome da mãe segue conferido")]
    [InlineData("NOME", "Maria S. Silva", true)]
    [InlineData("NOME", "Madonna", true)]
    [InlineData("NOME_MAE", "Maria S. Silva", false)]
    public void CampoDeNome_FormatoCongelado_SoConfereForaDoNomeCivil(string fato, string resposta, bool atende)
    {
        DefinicaoFormulario formulario = Formulario(
            new DefinicaoItem(fato, null, Obrigatoriedade.Sempre, [], formato: "NOME_PESSOA"));

        AvaliacaoFormulario avaliacao = AvaliarDefinicao(formulario, Entrada(paginaConcluida: true, (fato, resposta)));

        avaliacao.Itens[0].RestricoesVioladas.Select(static r => r.Tipo).Should().Equal(
            atende ? [] : [TipoRestricaoValor.FormatoTexto]);
    }

    [Fact]
    public void MunicipioDaUf_ComUfIndeterminada_FicaIndeterminado()
    {
        DefinicaoFormulario formulario = Formulario(
            Item("NATURALIDADE_UF"),
            Item("NATURALIDADE_MUNICIPIO", restricoes: [new MunicipiosDaUf("NATURALIDADE_UF")]));

        AvaliacaoFormulario avaliacao = AvaliarDefinicao(formulario, Entrada(paginaConcluida: false, ("NATURALIDADE_MUNICIPIO", "1504208")));

        avaliacao.Fatos["NATURALIDADE_MUNICIPIO"].Estado.Should().Be(EstadoFato.Indeterminado);
        avaliacao.Itens[1].RestricoesVioladas.Should().BeEmpty();
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

        AvaliacaoFormulario avaliacao = AvaliarDefinicao(
            formulario, Entrada(paginaConcluida: true, ("UF", "PA"), ("MUNICIPIO", "MANAUS")));

        avaliacao.Itens[1].RestricoesVioladas.Should().ContainSingle();
        avaliacao.Fatos["MUNICIPIO"].Estado.Should().Be(EstadoFato.Indeterminado, "o campo é obrigatório e a resposta não vale");
    }

    [Theory(DisplayName = "As opções vigentes do campo são as do filtro da resposta anterior; sem ela, ainda não são todas")]
    [InlineData("PA", new[] { "BELEM", "MARABA" }, true)]
    [InlineData(null, new string[0], false)]
    public void OpcoesVigentes_SeguemARespostaAnterior(string? uf, string[] esperadas, bool definitivas)
    {
        OpcoesPermitidas municipios = new(
        [
            new OpcoesCondicionadas(Predicado(Condicao("UF", Operador.Igual, "PA")), ["MARABA", "BELEM"]),
            new OpcoesCondicionadas(Predicado(Condicao("UF", Operador.Igual, "AM")), ["MANAUS"]),
        ]);
        DefinicaoFormulario formulario = Formulario(Item("UF"), Item("MUNICIPIO", restricoes: [municipios]));
        (string, object)[] respostas = uf is null ? [] : [("UF", uf)];

        AvaliacaoFormulario avaliacao = AvaliarDefinicao(formulario, Entrada(paginaConcluida: false, respostas));

        avaliacao.Itens[1].Opcoes!.Codigos.Should().Equal(esperadas);
        avaliacao.Itens[1].Opcoes!.Definitivas.Should().Be(definitivas);
        avaliacao.Itens[0].Opcoes.Should().BeNull("o campo sem restrição de opções não limita a escolha");
    }

    [Fact(DisplayName = "A entrada que ainda depende de resposta mas não traz opção nova não deixa as opções provisórias")]
    public void OpcoesVigentes_EntradaPendenteSemOpcaoNova_Definitivas()
    {
        OpcoesPermitidas opcoes = new(
        [
            new OpcoesCondicionadas(null, ["A", "B"]),
            new OpcoesCondicionadas(Predicado(Condicao("UF", Operador.Igual, "PA")), ["A"]),
        ]);
        DefinicaoFormulario formulario = Formulario(Item("UF"), Item("ESCOLHA", restricoes: [opcoes]));

        AvaliacaoFormulario avaliacao = AvaliarDefinicao(formulario, Entrada(paginaConcluida: false));

        avaliacao.Itens[1].Opcoes!.Codigos.Should().Equal("A", "B");
        avaliacao.Itens[1].Opcoes!.Definitivas.Should().BeTrue("a resposta pendente só poderia acrescentar A, que já vale");
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

        AvaliacaoFormulario avaliacao = AvaliarDefinicao(
            formulario, Entrada(paginaConcluida: false, ("LOCAL", JsonDocument.Parse(municipio).RootElement)));

        avaliacao.Fatos["LOCAL"].Estado.Should().Be(esperado);
        avaliacao.Itens[1].RestricoesVioladas.Should().BeEmpty();
    }
}
