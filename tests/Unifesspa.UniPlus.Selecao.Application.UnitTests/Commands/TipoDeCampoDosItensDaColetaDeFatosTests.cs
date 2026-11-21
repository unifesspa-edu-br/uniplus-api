namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ApoioDaColetaDeFatos;

/// <summary>A leitura do tipo de campo que o pedido informa para cada item do formulário do processo.</summary>
public sealed class TipoDeCampoDosItensDaColetaDeFatosTests
{
    [Fact(DisplayName = "Tipo de campo ausente é recusado como obrigatório, no campo do item")]
    public async Task Handle_TipoDeCampoAusente_RecusaComoObrigatorio()
    {
        Result resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(), [new FatoColetadoInput("BAIXA_RENDA", 0, "Baixa renda", null, "SEMPRE", null)]);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("itens[0].tipoRenderizacao", "ItemFormulario.TipoRenderizacaoObrigatorio"));
    }

    [Fact(DisplayName = "Tipo de campo desconhecido é recusado como inválido e a coerência com o fato não roda")]
    public async Task Handle_TipoDeCampoDesconhecido_RecusaComoInvalidoSemConferirACoerencia()
    {
        Result resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(), [new FatoColetadoInput("BAIXA_RENDA", 0, "Baixa renda", "NENHUMA", "SEMPRE", null)]);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("itens[0].tipoRenderizacao", "ItemFormulario.TipoRenderizacaoInvalido"));
    }

    [Fact(DisplayName = "Tipo de campo desconhecido acumula com a outra recusa do mesmo item")]
    public async Task Handle_TipoDeCampoDesconhecidoEOrdemNegativa_AcumulaAsDuasRecusas()
    {
        Result resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(), [new FatoColetadoInput("BAIXA_RENDA", -1, "Baixa renda", "boolean", "SEMPRE", null)]);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("itens[0].ordem", "ItemFormulario.OrdemInvalida"),
            ("itens[0].tipoRenderizacao", "ItemFormulario.TipoRenderizacaoInvalido"),
        ]);
    }

    [Fact(DisplayName = "Tipo de campo desconhecido de um item não esconde a coerência do tipo de outro")]
    public async Task Handle_TipoDeCampoDesconhecidoEmUmItemEIncoerenteNoOutro_AcumulaAsDuas()
    {
        Result resultado = await DefinirNaHabilitacaoAsync(
            ProcessoEmRascunho(),
            [
                new FatoColetadoInput("BAIXA_RENDA", 0, "Baixa renda", "NENHUMA", "SEMPRE", null),
                new FatoColetadoInput("COR_RACA", 1, "Cor ou raça", "BOOLEANO", "SEMPRE", null),
            ]);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("itens[0].tipoRenderizacao", "ItemFormulario.TipoRenderizacaoInvalido"),
            ("itens[1].tipoRenderizacao", "ItemFormulario.TipoRenderizacaoIncoerenteComDominio"),
        ]);
    }
}
