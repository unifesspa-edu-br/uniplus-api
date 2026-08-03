namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;
using Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class MarcarVigenteCalendarioDiasUteisCommandHandlerTests
{
    private readonly ICalendarioDiasUteisRepository _repository = Substitute.For<ICalendarioDiasUteisRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static CalendarioDiasUteis Novo(string versaoDataset = "2027.1") =>
        CalendarioDiasUteis.Criar(
            versaoDataset,
            [new DiaNaoUtilCriacao("NACIONAL", null, new DateOnly(2027, 1, 1), "Confraternização Universal")]).Value!;

    [Fact(DisplayName = "Id inexistente retorna NaoEncontrado")]
    public async Task Handle_NaoEncontrado_RetornaErro()
    {
        Guid id = Guid.NewGuid();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((CalendarioDiasUteis?)null);

        Result resultado = await MarcarVigenteCalendarioDiasUteisCommandHandler.Handle(
            new MarcarVigenteCalendarioDiasUteisCommand(id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.NaoEncontrado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Dataset já vigente falha (JaVigente)")]
    public async Task Handle_JaVigente_RetornaErro()
    {
        CalendarioDiasUteis calendario = Novo();
        calendario.MarcarVigente();
        _repository.ObterPorIdAsync(calendario.Id, Arg.Any<CancellationToken>()).Returns(calendario);

        Result resultado = await MarcarVigenteCalendarioDiasUteisCommandHandler.Handle(
            new MarcarVigenteCalendarioDiasUteisCommand(calendario.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.JaVigente);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Dataset não vigente sem vigente anterior fica vigente e persiste")]
    public async Task Handle_SemVigenteAnterior_MarcaVigenteEPersiste()
    {
        CalendarioDiasUteis calendario = Novo();
        _repository.ObterPorIdAsync(calendario.Id, Arg.Any<CancellationToken>()).Returns(calendario);
        _repository.ObterVigenteAsync(Arg.Any<CancellationToken>()).Returns((CalendarioDiasUteis?)null);

        Result resultado = await MarcarVigenteCalendarioDiasUteisCommandHandler.Handle(
            new MarcarVigenteCalendarioDiasUteisCommand(calendario.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        calendario.Vigente.Should().BeTrue();
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Dataset não vigente com vigente anterior desmarca o anterior e marca o novo")]
    public async Task Handle_ComVigenteAnterior_DesmarcaAnteriorEMarcaNovo()
    {
        CalendarioDiasUteis novo = Novo("2027.1");
        CalendarioDiasUteis vigenteAnterior = Novo("2026.2");
        vigenteAnterior.MarcarVigente();

        _repository.ObterPorIdAsync(novo.Id, Arg.Any<CancellationToken>()).Returns(novo);
        _repository.ObterVigenteAsync(Arg.Any<CancellationToken>()).Returns(vigenteAnterior);

        Result resultado = await MarcarVigenteCalendarioDiasUteisCommandHandler.Handle(
            new MarcarVigenteCalendarioDiasUteisCommand(novo.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        novo.Vigente.Should().BeTrue();
        vigenteAnterior.Vigente.Should().BeFalse();
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Exclusion constraint de vigência colidindo na checagem forçada vira ConflitoDeConcorrencia")]
    public async Task Handle_ColisaoNaExclusionConstraint_RetornaConflitoDeConcorrencia()
    {
        // A violação surge em ForcarChecagemImediataDeConstraintsAsync (SET CONSTRAINTS
        // IMMEDIATE), não em SalvarAlteracoesAsync — a constraint é DEFERRABLE INITIALLY
        // DEFERRED, então SaveChangesAsync sozinho não a checaria (só o faria no commit
        // externo do outbox do Wolverine, fora do try do handler).
        CalendarioDiasUteis calendario = Novo();
        _repository.ObterPorIdAsync(calendario.Id, Arg.Any<CancellationToken>()).Returns(calendario);
        _repository.ObterVigenteAsync(Arg.Any<CancellationToken>()).Returns((CalendarioDiasUteis?)null);
        _unitOfWork.ForcarChecagemImediataDeConstraintsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(PostgresExceptionFactory.Create("23P01", "ex_calendario_dias_uteis_vigente_unico"));

        Result resultado = await MarcarVigenteCalendarioDiasUteisCommandHandler.Handle(
            new MarcarVigenteCalendarioDiasUteisCommand(calendario.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.ConflitoDeConcorrencia);
        _unitOfWork.DidNotReceive().DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "DbUpdateConcurrencyException (xmin) no commit descarta o rastreamento antes de devolver 409")]
    public async Task Handle_ConcorrenciaOtimista_DescartaRastreamentoERetornaConflito()
    {
        // Endpoint tem [RequiresIdempotencyKey] (ADR-0119): a exceção não pode
        // propagar sem catch — IdempotencyFilter não verifica
        // ResourceExecutedContext.Exception antes de cachear a resposta (#1028).
        CalendarioDiasUteis calendario = Novo();
        _repository.ObterPorIdAsync(calendario.Id, Arg.Any<CancellationToken>()).Returns(calendario);
        _repository.ObterVigenteAsync(Arg.Any<CancellationToken>()).Returns((CalendarioDiasUteis?)null);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateConcurrencyException("conflito sintético de teste"));

        Result resultado = await MarcarVigenteCalendarioDiasUteisCommandHandler.Handle(
            new MarcarVigenteCalendarioDiasUteisCommand(calendario.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.ConflitoDeConcorrencia);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Violação de outra constraint não é engolida — propaga")]
    public async Task Handle_OutraViolacaoDeConstraint_Propaga()
    {
        CalendarioDiasUteis calendario = Novo();
        _repository.ObterPorIdAsync(calendario.Id, Arg.Any<CancellationToken>()).Returns(calendario);
        _repository.ObterVigenteAsync(Arg.Any<CancellationToken>()).Returns((CalendarioDiasUteis?)null);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(PostgresExceptionFactory.Create("23505", "outra_constraint_qualquer"));

        Func<Task> act = () => MarcarVigenteCalendarioDiasUteisCommandHandler.Handle(
            new MarcarVigenteCalendarioDiasUteisCommand(calendario.Id), _repository, _unitOfWork, CancellationToken.None);

        await act.Should().ThrowAsync<Npgsql.PostgresException>();
    }
}
