namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DefinirOpcoesDeCursoCommandHandlerTests
{
    private static (IProcessoSeletivoRepository Repository, ISelecaoUnitOfWork UnitOfWork) NovosMocks(ProcessoSeletivo? processo, Guid processoId)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);
        return (repository, Substitute.For<ISelecaoUnitOfWork>());
    }

    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        (IProcessoSeletivoRepository repository, ISelecaoUnitOfWork unitOfWork) = NovosMocks(null, Guid.CreateVersion7());

        Result<MutacaoAceita> result = await DefinirOpcoesDeCursoCommandHandler.Handle(
            new DefinirOpcoesDeCursoCommand(Guid.CreateVersion7(), 1, PrecondicaoIfMatch.Ausente), repository, unitOfWork, CancellationToken.None);

        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Handle com 1 opção sobre 1 curso grava e persiste")]
    public async Task Handle_UmaOpcaoComUmCurso_GravaEPersiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS opções", out _);
        (IProcessoSeletivoRepository repository, ISelecaoUnitOfWork unitOfWork) = NovosMocks(processo, processo.Id);

        Result<MutacaoAceita> result = await DefinirOpcoesDeCursoCommandHandler.Handle(
            new DefinirOpcoesDeCursoCommand(processo.Id, 1, PrecondicaoIfMatch.Ausente), repository, unitOfWork, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processo.NumeroDeOpcoesDeCurso.Should().Be(1);
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com 2 opções sobre 1 curso recusa no campo numero e não persiste")]
    public async Task Handle_DuasOpcoesComUmCurso_RecusaENaoPersiste()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS opções", out _);
        (IProcessoSeletivoRepository repository, ISelecaoUnitOfWork unitOfWork) = NovosMocks(processo, processo.Id);

        Result<MutacaoAceita> result = await DefinirOpcoesDeCursoCommandHandler.Handle(
            new DefinirOpcoesDeCursoCommand(processo.Id, 2, PrecondicaoIfMatch.Ausente), repository, unitOfWork, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle(e =>
            e.Field == "numero" && e.Error.Code == "ProcessoSeletivo.OpcoesDeCursoAcimaDasOfertas");
        processo.NumeroDeOpcoesDeCurso.Should().Be(1, "a recusa mantém a declaração anterior");
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com conflito de concorrência descarta o rastreamento e devolve o conflito, sem relançar")]
    public async Task Handle_ConflitoDeConcorrencia_DescartaEDevolveConflito()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS opções", out _);
        (IProcessoSeletivoRepository repository, ISelecaoUnitOfWork unitOfWork) = NovosMocks(processo, processo.Id);
        unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ => throw new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException("xmin"));

        Result<MutacaoAceita> result = await DefinirOpcoesDeCursoCommandHandler.Handle(
            new DefinirOpcoesDeCursoCommand(processo.Id, 1, PrecondicaoIfMatch.Ausente), repository, unitOfWork, CancellationToken.None);

        result.Error!.Code.Should().Be("ProcessoSeletivo.ConflitoDeConcorrencia");
        unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }
}
