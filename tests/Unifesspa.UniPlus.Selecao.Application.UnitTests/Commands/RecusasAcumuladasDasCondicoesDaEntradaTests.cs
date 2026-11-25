namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ApoioDaColetaDeFatos;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ApoioDosDocumentosExigidos;

/// <summary>
/// As condições vindas do pedido — pré-condição do item e gatilho da exigência — acumulam as recusas de todas as
/// condições, cada uma no caminho da condição e no campo de origem.
/// </summary>
public sealed class RecusasAcumuladasDasCondicoesDaEntradaTests
{
    private const string FatoNaoInformado = "CondicaoDnf.FatoObrigatorio";
    private const string OperadorDesconhecido = "CondicaoDnf.OperadorInvalido";

    private static Task<Result> DefinirItemComPrecondicaoAsync(params IReadOnlyList<CondicaoPrecondicaoInput>[] clausulas)
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        return DefinirNaHabilitacaoAsync(
            processo,
            [EntradaDoFatoColetadoDeTeste.Coletando("BAIXA_RENDA").ComRotulo("Baixa renda").ComObrigatoriedade("NUNCA").ExibidoQuando(clausulas).NaSecao(FormularioDeTeste.Secao).Construir()]);
    }

    [Fact(DisplayName = "Pré-condição com fato vazio e operador desconhecido na mesma condição recusa os dois campos")]
    public async Task DefinirFatosColetados_PrecondicaoComFatoVazioEOperadorDesconhecido_AcumulaOsDoisCampos()
    {
        Result resultado = await DefinirItemComPrecondicaoAsync([Condicao("", "XPTO", true)]);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Contain(
        [
            ("itens[0].regras.exibicao[0][0].fato", FatoNaoInformado),
            ("itens[0].regras.exibicao[0][0].operador", OperadorDesconhecido),
        ]);
    }

    [Fact(DisplayName = "Pré-condição com duas condições inválidas recusa cada uma na sua posição")]
    public async Task DefinirFatosColetados_PrecondicaoComDuasCondicoesInvalidas_RecusaCadaUmaNaSuaPosicao()
    {
        Result resultado = await DefinirItemComPrecondicaoAsync(
            [Condicao("", "IGUAL", true)], [Condicao("COR_RACA", "IGUAL", "PRETA"), Condicao("COR_RACA", "XPTO", "PRETA")]);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Contain(
        [
            ("itens[0].regras.exibicao[0][0].fato", FatoNaoInformado),
            ("itens[0].regras.exibicao[1][1].operador", OperadorDesconhecido),
        ]);
    }

    [Fact]
    public async Task DefinirFatosColetados_ImpedimentoComDuasCondicoesInvalidas_RecusaCadaPosicao()
    {
        ImpedimentoInput impedimento = new([[Condicao("", "IGUAL", true), Condicao("BAIXA_RENDA", "XPTO", true)]], "Não permitido");
        FatoColetadoInput item = EntradaDoFatoColetadoDeTeste.Coletando("BAIXA_RENDA")
            .ComObrigatoriedade("NUNCA").ComImpedimento(impedimento).NaSecao(FormularioDeTeste.Secao).Construir();

        Result resultado = await DefinirNaHabilitacaoAsync(ProcessoEmRascunho(), [item]);

        resultado.Errors.Select(static e => e.Field).Should().Contain(
            ["itens[0].regras.impedimento.quando[0][0].fato", "itens[0].regras.impedimento.quando[0][1].operador"]);
    }

    [Fact]
    public async Task DefinirFatosColetados_PredicadoDaObrigatoriedadeInvalido_ApontaOCampo()
    {
        FatoColetadoInput item = EntradaDoFatoColetadoDeTeste.Coletando("BAIXA_RENDA")
            .ComObrigatoriedade("QUANDO", [[Condicao("", "IGUAL", true)]])
            .NaSecao(FormularioDeTeste.Secao).Construir();

        Result resultado = await DefinirNaHabilitacaoAsync(ProcessoEmRascunho(), [item]);

        resultado.Errors.Should().Contain(e => e.Field == "itens[0].regras.obrigatoriedade.predicado[0][0].fato");
    }

    [Fact(DisplayName = "Gatilho da exigência com fato vazio e operador desconhecido na mesma condição recusa os dois campos")]
    public async Task DefinirDocumentosExigidos_GatilhoComFatoVazioEOperadorDesconhecido_AcumulaOsDoisCampos()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();

        Result resultado = await cenario.DefinirAsync(cenario.ItemCondicional(new(0, "", "XPTO", "true")));

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Contain(
        [
            ("regrasDeComprovacao[0].documento.aplicabilidade.condicoes[0].fato", FatoNaoInformado),
            ("regrasDeComprovacao[0].documento.aplicabilidade.condicoes[0].operador", OperadorDesconhecido),
        ]);
    }

    [Fact(DisplayName = "Gatilho da exigência com duas condições inválidas recusa cada uma na sua posição")]
    public async Task DefinirDocumentosExigidos_GatilhoComDuasCondicoesInvalidas_RecusaCadaUmaNaSuaPosicao()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        ItemDocumentoExigidoDeTeste item = cenario.ItemGeral().Condicional(new(0, "", "IGUAL", "true"), new(1, "SEXO", "XPTO", "true"));

        Result resultado = await cenario.DefinirAsync(item);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Contain(
        [
            ("regrasDeComprovacao[0].documento.aplicabilidade.condicoes[0].fato", FatoNaoInformado),
            ("regrasDeComprovacao[0].documento.aplicabilidade.condicoes[1].operador", OperadorDesconhecido),
        ]);
    }
}
