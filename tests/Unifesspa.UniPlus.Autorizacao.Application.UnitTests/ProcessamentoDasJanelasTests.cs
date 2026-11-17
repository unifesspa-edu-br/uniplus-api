namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class ProcessamentoDasJanelasTests
{
    private readonly IJanelaDeMigracaoRepository _janelas = Substitute.For<IJanelaDeMigracaoRepository>();
    private readonly ICommandBus _bus = Substitute.For<ICommandBus>();
    private readonly IExecucaoUnicaDaRotina _execucaoUnica = Substitute.For<IExecucaoUnicaDaRotina>();

    [Fact(DisplayName = "A rotina conta só os perfis tratados e a falha de um não impede os demais")]
    public async Task Executar_FalhaDeUmPerfil_NaoImpedeOsDemais()
    {
        Guid tratado = Guid.NewGuid();
        Guid comFalha = Guid.NewGuid();
        Guid semNada = Guid.NewGuid();
        _execucaoUnica.TentarIniciarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IAsyncDisposable?>(Substitute.For<IAsyncDisposable>()));
        _janelas.ListarPerfisComJanelaNaoEncerradaAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Guid>>([comFalha, tratado, semNada]));
        _bus.Send(new MaterializarJanelaVencidaCommand(comFalha), Arg.Any<CancellationToken>()).Returns<Task<Result<bool>>>(_ => throw new InvalidOperationException("Falha de teste."));
        _bus.Send(new MaterializarJanelaVencidaCommand(tratado), Arg.Any<CancellationToken>()).Returns(Task.FromResult(Result<bool>.Success(true)));
        _bus.Send(new MaterializarJanelaVencidaCommand(semNada), Arg.Any<CancellationToken>()).Returns(Task.FromResult(Result<bool>.Success(false)));

        int processadas = await Rotina().ExecutarAsync(CancellationToken.None);

        processadas.Should().Be(1);
    }

    [Fact(DisplayName = "A rotina com outra instância em execução pula a execução sem ler as janelas")]
    public async Task Executar_OutraInstancia_PulaAExecucao()
    {
        _execucaoUnica.TentarIniciarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IAsyncDisposable?>(null));

        int processadas = await Rotina().ExecutarAsync(CancellationToken.None);

        processadas.Should().Be(0);
        await _janelas.DidNotReceiveWithAnyArgs().ListarPerfisComJanelaNaoEncerradaAsync(default);
    }

    private ProcessamentoDasJanelas Rotina() => new(_janelas, _bus, _execucaoUnica, NullLogger<ProcessamentoDasJanelas>.Instance);
}
