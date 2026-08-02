namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class RemoverCalendarioDiasUteisCommandHandlerTests
{
    private readonly ICalendarioDiasUteisRepository _repository = Substitute.For<ICalendarioDiasUteisRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static CalendarioDiasUteis Novo() =>
        CalendarioDiasUteis.Criar(
            "2027.1",
            [new DiaNaoUtilCriacao("NACIONAL", null, new DateOnly(2027, 1, 1), "Confraternização Universal")]).Value!;

    [Fact(DisplayName = "Id inexistente retorna NaoEncontrado")]
    public async Task Handle_NaoEncontrado_RetornaErro()
    {
        Guid id = Guid.NewGuid();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((CalendarioDiasUteis?)null);

        Result resultado = await RemoverCalendarioDiasUteisCommandHandler.Handle(
            new RemoverCalendarioDiasUteisCommand(id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.NaoEncontrado);
        _repository.DidNotReceive().Remover(Arg.Any<CalendarioDiasUteis>());
    }

    [Fact(DisplayName = "Dataset vigente não pode ser removido (NaoRemoveVigente)")]
    public async Task Handle_DatasetVigente_RetornaErroSemRemover()
    {
        CalendarioDiasUteis calendario = Novo();
        calendario.MarcarVigente();
        _repository.ObterPorIdAsync(calendario.Id, Arg.Any<CancellationToken>()).Returns(calendario);

        Result resultado = await RemoverCalendarioDiasUteisCommandHandler.Handle(
            new RemoverCalendarioDiasUteisCommand(calendario.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.NaoRemoveVigente);
        _repository.DidNotReceive().Remover(Arg.Any<CalendarioDiasUteis>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Dataset não vigente é removido e persistido")]
    public async Task Handle_DatasetNaoVigente_RemoveEPersiste()
    {
        CalendarioDiasUteis calendario = Novo();
        _repository.ObterPorIdAsync(calendario.Id, Arg.Any<CancellationToken>()).Returns(calendario);

        Result resultado = await RemoverCalendarioDiasUteisCommandHandler.Handle(
            new RemoverCalendarioDiasUteisCommand(calendario.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        _repository.Received(1).Remover(calendario);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Remoção colidindo com ativação concorrente do mesmo dataset (xmin) vira ConflitoDeConcorrencia")]
    public async Task Handle_ConcorrenciaOtimista_RetornaConflitoDeConcorrencia()
    {
        CalendarioDiasUteis calendario = Novo();
        _repository.ObterPorIdAsync(calendario.Id, Arg.Any<CancellationToken>()).Returns(calendario);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateConcurrencyException("conflito sintético de teste"));

        Result resultado = await RemoverCalendarioDiasUteisCommandHandler.Handle(
            new RemoverCalendarioDiasUteisCommand(calendario.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.ConflitoDeConcorrencia);
    }
}
