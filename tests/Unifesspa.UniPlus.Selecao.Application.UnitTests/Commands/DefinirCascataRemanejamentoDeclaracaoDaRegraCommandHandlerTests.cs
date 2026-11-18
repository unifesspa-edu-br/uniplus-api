namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ApoioDaCascataDeRemanejamento;

public sealed class DefinirCascataRemanejamentoDeclaracaoDaRegraCommandHandlerTests
{
    [Fact(DisplayName = "Handle com fallback divergente da declaração da regra recusa com MatrizDivergenteDaRegra, mesmo com forma válida")]
    public async Task Handle_FallbackDivergenteDoDeclaracaoDaRegra_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        ConfigurarRegra(mocks, RegraCascataValida());

        DefinirCascataRemanejamentoCommand command = new(
            processo.Id, RegraRemanejamentoCodigo.Cascata, "v1", "AMPLA",
            [new DestinoRemanejamentoInput("LB_PPI", 1, "LB_Q"), new DestinoRemanejamentoInput("LB_PPI", 2, "LB_PCD")]);

        Result result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.MatrizDivergenteDaRegra");
        result.Error.Message.Should().NotContain(command.RegraCodigo!);
    }

    [Fact(DisplayName = "Handle com destino trocado em relação à declaração da regra recusa com MatrizDivergenteDaRegra")]
    public async Task Handle_DestinoTrocadoNoDeclaracaoDaRegra_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        ConfigurarRegra(mocks, RegraCascataValida());

        DefinirCascataRemanejamentoCommand command = new(
            processo.Id, RegraRemanejamentoCodigo.Cascata, "v1", "AC",
            [new DestinoRemanejamentoInput("LB_PPI", 1, "LB_Q"), new DestinoRemanejamentoInput("LB_PPI", 2, "LB_EP")]);

        Result result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.MatrizDivergenteDaRegra");
    }

    [Fact(DisplayName = "Handle com linha de origem omitida em relação à declaração da regra recusa com MatrizDivergenteDaRegra")]
    public async Task Handle_OrigemOmitidaNoDeclaracaoDaRegra_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        ConfigurarRegra(mocks, RegraComDuasOrigens());

        DefinirCascataRemanejamentoCommand command = new(
            processo.Id, RegraRemanejamentoCodigo.Cascata, "v1", "AC",
            [new DestinoRemanejamentoInput("LB_PPI", 1, "LB_Q")]);

        Result result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.MatrizDivergenteDaRegra");
    }

    [Fact(DisplayName = "Handle com declaração da regra repetindo uma origem não deixa outra origem sem conferir")]
    public async Task Handle_DeclaracaoDaRegraComOrigemRepetida_NaoCobreOrigemNaoConferida()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        ConfigurarRegra(mocks, RegraComOrigemRepetida());

        DefinirCascataRemanejamentoCommand command = new(
            processo.Id, RegraRemanejamentoCodigo.Cascata, "v1", "AC",
            [new DestinoRemanejamentoInput("LB_PPI", 1, "LB_Q"), new DestinoRemanejamentoInput("LB_Q", 1, "DESTINO_NUNCA_CONFERIDO")]);

        Result result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue("LB_Q nunca foi comparado contra a declaração da regra — a origem repetida não pode cobri-la");
        result.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.MatrizDivergenteDaRegra");
    }

    [Fact(DisplayName = "Handle com item nulo na lista de destinos recusa com CamposObrigatorios, sem lançar")]
    public async Task Handle_ItemNuloEmDestinos_RecusaSemLancar()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Mocks mocks = NovosMocks(processo, processo.Id);
        ConfigurarRegra(mocks, RegraCascataValida());

        DefinirCascataRemanejamentoCommand command = new(
            processo.Id, RegraRemanejamentoCodigo.Cascata, "v1", "AC",
            [null!]);

        Result result = await DefinirCascataRemanejamentoCommandHandler.Handle(
            command, mocks.Repository, mocks.RegraCatalogoReader, mocks.UnitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoCascataRemanejamento.CamposObrigatorios");
    }
}
