namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ApoioDaColetaDeFatos;

/// <summary>
/// A ordem do item e do grupo, e a lista de campos do grupo, ausentes no corpo chegam ao domínio e são recusadas com o código de campo
/// obrigatório, no campo do pedido, junto das demais recusas; a ordem zero continua sendo uma ordem válida.
/// </summary>
public sealed class DefinirFatosColetadosOrdemEGrupoAusentesTests
{
    private static FatoColetadoInput ItemDeCorRaca() =>
        EntradaDoFatoColetadoDeTeste.Coletando("COR_RACA").ComRotulo("Cor ou raça").DoTipo("ESCOLHA_UNICA").ComObrigatoriedade("NUNCA").Construir();

    private static DefinirFatosColetadosCommand Comando(
        ProcessoSeletivo processo, IReadOnlyList<FatoColetadoInput> itens, IReadOnlyList<GrupoColetadoInput>? grupos = null) =>
        new(processo.Id, PapelDoFormularioDeTeste.Habilitacao.Codigo(), itens, grupos);

    private static (string? Campo, string Codigo)[] Recusas(Result resultado) =>
        [.. resultado.Errors.Select(static e => (e.Field, e.Error.Code))];

    [Fact(DisplayName = "O item sem ordem é recusado como obrigatório e nada é gravado")]
    public async Task Handle_ItemSemOrdem_RecusaComoObrigatorioESemGravar()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);

        Result resultado = await HandleAsync(mocks, Comando(processo, [ItemDeCorRaca() with { Ordem = null }]));

        Recusas(resultado).Should().Equal(("itens[0].ordem", CodigosDeValidacao.Obrigatorio));
        processo.FatosColetados.Should().BeEmpty();
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O item com ordem zero é gravado")]
    public async Task Handle_ItemComOrdemZero_Grava()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();

        Result resultado = await HandleAsync(NovosMocks(processo, processo.Id), Comando(processo, [ItemDeCorRaca() with { Ordem = 0 }]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.FatosColetados.Single().Ordem.Should().Be(0);
    }

    [Fact(DisplayName = "O grupo sem ordem e sem campos sai com as duas recusas obrigatórias, cada uma no seu campo")]
    public async Task Handle_GrupoSemOrdemESemCampos_RecusaAsDuas()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        GrupoColetadoInput grupo = EntradaDoGrupoColetadoDeTeste.Compondo("COMPOSICAO_FAMILIAR", 1).ComRotulo("Composição familiar")
            .ComOcorrencias(0, 10).ComCampos(EntradaDoFatoColetadoDeTeste.Coletando("PARENTESCO").ComRotulo("Parentesco").DoTipo("ESCOLHA_UNICA").Construir())
            .Construir() with
        { Ordem = null, Subitens = null };

        Result resultado = await HandleAsync(NovosMocks(processo, processo.Id), Comando(processo, [], [grupo]));

        Recusas(resultado).Should().Equal(
            ("grupos[0].ordem", CodigosDeValidacao.Obrigatorio), ("grupos[0].subitens", CodigosDeValidacao.Obrigatorio));
    }
}
