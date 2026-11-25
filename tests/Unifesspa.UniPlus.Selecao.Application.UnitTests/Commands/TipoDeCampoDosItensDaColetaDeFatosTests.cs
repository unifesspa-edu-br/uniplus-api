namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ApoioDaColetaDeFatos;

/// <summary>A leitura do tipo de campo que o pedido informa para cada item do formulário do processo.</summary>
public sealed class TipoDeCampoDosItensDaColetaDeFatosTests
{
    [Fact(DisplayName = "Tipo de campo ausente é recusado como obrigatório, no campo do item")]
    public async Task Handle_TipoDeCampoAusente_RecusaComoObrigatorio()
    {
        Result resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(), [EntradaDoFatoColetadoDeTeste.Coletando("BAIXA_RENDA").ComRotulo("Baixa renda").DoTipo(null).Construir()]);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("itens[0].apresentacao.tipoDeCampo", "ItemFormulario.TipoDeCampoObrigatorio"));
    }

    [Fact(DisplayName = "Tipo de campo desconhecido é recusado como inválido e a coerência com o fato não roda")]
    public async Task Handle_TipoDeCampoDesconhecido_RecusaComoInvalidoSemConferirACoerencia()
    {
        Result resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(), [EntradaDoFatoColetadoDeTeste.Coletando("BAIXA_RENDA").ComRotulo("Baixa renda").DoTipo("NENHUMA").Construir()]);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("itens[0].apresentacao.tipoDeCampo", "ItemFormulario.TipoDeCampoInvalido"));
    }

    [Fact(DisplayName = "Tipo de campo desconhecido acumula com a outra recusa do mesmo item")]
    public async Task Handle_TipoDeCampoDesconhecidoEOrdemNegativa_AcumulaAsDuasRecusas()
    {
        Result resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(), [EntradaDoFatoColetadoDeTeste.Coletando("BAIXA_RENDA", -1).ComRotulo("Baixa renda").DoTipo("boolean").Construir()]);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("itens[0].ordem", "ItemFormulario.OrdemInvalida"),
            ("itens[0].apresentacao.tipoDeCampo", "ItemFormulario.TipoDeCampoInvalido"),
        ]);
    }

    [Fact(DisplayName = "Tipo de campo desconhecido de um item não esconde a coerência do tipo de outro")]
    public async Task Handle_TipoDeCampoDesconhecidoEmUmItemEIncoerenteNoOutro_AcumulaAsDuas()
    {
        Result resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(),
            [
                EntradaDoFatoColetadoDeTeste.Coletando("BAIXA_RENDA").ComRotulo("Baixa renda").DoTipo("NENHUMA").Construir(),
                EntradaDoFatoColetadoDeTeste.Coletando("COR_RACA", 1).ComRotulo("Cor ou raça").Construir(),
            ]);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("itens[0].apresentacao.tipoDeCampo", "ItemFormulario.TipoDeCampoInvalido"),
            ("itens[1].apresentacao.tipoDeCampo", "ItemFormulario.TipoDeCampoIncoerenteComDominio"),
        ]);
    }

    [Fact(DisplayName = "Apresentação ausente é recusada pelo rótulo e pelo tipo de campo obrigatórios, no caminho dela")]
    public async Task Handle_ApresentacaoAusente_RecusaRotuloETipoNoCaminhoDaApresentacao()
    {
        Result resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(), [EntradaDoFatoColetadoDeTeste.Coletando("BAIXA_RENDA").SemApresentacao().Construir()]);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("itens[0].apresentacao.rotulo", "ItemFormulario.RotuloObrigatorio"),
            ("itens[0].apresentacao.tipoDeCampo", "ItemFormulario.TipoDeCampoObrigatorio"),
        ]);
    }
}
