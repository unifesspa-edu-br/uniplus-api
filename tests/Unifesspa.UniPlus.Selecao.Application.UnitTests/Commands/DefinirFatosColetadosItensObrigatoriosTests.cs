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
/// A lista de itens é obrigatória no corpo: a ausente chega ao domínio, é recusada com o código de campo obrigatório e acumula
/// com as demais recusas (ADR-0125). A lista vazia declara nenhum item e os remove.
/// </summary>
public sealed class DefinirFatosColetadosItensObrigatoriosTests
{
    [Fact(DisplayName = "Lista de itens ausente é recusada no campo com o código de campo obrigatório e nada é gravado")]
    public async Task Handle_ItensAusentes_RecusaNoCampoESemGravar()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);

        Result resultado = await HandleAsync(mocks, ComItens(processo, itens: null));

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("itens", CodigosDeValidacao.Obrigatorio));
        await mocks.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Lista de itens ausente não apaga os itens já gravados do formulário")]
    public async Task Handle_ItensAusentes_MantemOsItensGravados()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        (await DefinirNaHabilitacaoAsync(processo, [ItemDeCorRaca()])).IsSuccess.Should().BeTrue();

        Result resultado = await HandleAsync(NovosMocks(processo, processo.Id), ComItens(processo, itens: null));

        resultado.IsFailure.Should().BeTrue();
        processo.FatosColetados.Select(static f => f.FatoCodigo).Should().Equal("COR_RACA");
    }

    [Fact(DisplayName = "Lista de itens vazia declara nenhum item e remove os gravados")]
    public async Task Handle_ItensVazios_RemoveOsItensGravados()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        (await DefinirNaHabilitacaoAsync(processo, [ItemDeCorRaca()])).IsSuccess.Should().BeTrue();

        Result resultado = await HandleAsync(NovosMocks(processo, processo.Id), ComItens(processo, itens: []));

        resultado.IsSuccess.Should().BeTrue();
        processo.FatosColetados.Should().BeEmpty();
    }

    [Fact(DisplayName = "Lista de itens ausente e grupo recusado saem juntos, a lista primeiro")]
    public async Task Handle_ItensAusentesEGrupoRecusado_AcumulaNaOrdem()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        GrupoColetadoInput grupoSemRotulo = EntradaDoGrupoColetadoDeTeste.Compondo("COMPOSICAO_FAMILIAR", 1).ComRotulo("")
            .ComOcorrencias(0, 10).ComCampos(EntradaDoFatoColetadoDeTeste.Coletando("PARENTESCO").ComRotulo("Parentesco").DoTipo("ESCOLHA_UNICA").Construir())
            .Construir();

        Result resultado = await HandleAsync(NovosMocks(processo, processo.Id), ComItens(processo, itens: null, [grupoSemRotulo]));

        resultado.Errors.Select(static e => e.Field).Should().Equal("itens", "grupos[0].rotulo");
    }

    [Fact(DisplayName = "No formulário de inscrição, a lista de itens ausente não recebe a mescla do conjunto básico nem recusas dela")]
    public async Task Handle_ItensAusentesNaInscricao_RecusaSoALista()
    {
        ProcessoSeletivo processo = ProcessoComInscricao();
        DefinirFatosColetadosCommand command = new(processo.Id, PapelDoFormularioDeTeste.Inscricao.Codigo(), Itens: null);

        Result resultado = await HandleAsync(NovosMocks(processo, processo.Id), command);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("itens", CodigosDeValidacao.Obrigatorio));
    }

    private static FatoColetadoInput ItemDeCorRaca() =>
        EntradaDoFatoColetadoDeTeste.Coletando("COR_RACA").ComRotulo("Cor ou raça").DoTipo("ESCOLHA_UNICA").ComObrigatoriedade("NUNCA").Construir();

    private static DefinirFatosColetadosCommand ComItens(
        ProcessoSeletivo processo, IReadOnlyList<FatoColetadoInput>? itens, IReadOnlyList<GrupoColetadoInput>? grupos = null) =>
        new(processo.Id, PapelDoFormularioDeTeste.Habilitacao.Codigo(), itens, grupos);
}
