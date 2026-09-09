namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class IncluirDiaNaoUtilCommandHandlerTests
{
    private readonly ICalendarioDiasUteisRepository _repository = Substitute.For<ICalendarioDiasUteisRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static CalendarioDiasUteis CalendarioExistente() =>
        CalendarioDiasUteis.Criar(
            "2027.1",
            [new DiaNaoUtilCriacao("NACIONAL", null, null, null, new DateOnly(2027, 1, 1), "Confraternização Universal")]).Value!;

    private static DiaNaoUtilCommandItem ItemValido() =>
        new("ESTADUAL", null, null, null, new DateOnly(2027, 1, 25), "Data magna do estado", "PA");

    [Fact(DisplayName = "Comando válido inclui a data, persiste e retorna o DTO atualizado")]
    public async Task Handle_ComandoValido_IncluiEPersiste()
    {
        CalendarioDiasUteis calendario = CalendarioExistente();
        _repository.ObterPorIdAsync(calendario.Id, Arg.Any<CancellationToken>()).Returns(calendario);

        Result<CalendarioDiasUteisDto> resultado = await IncluirDiaNaoUtilCommandHandler.Handle(
            new IncluirDiaNaoUtilCommand(calendario.Id, ItemValido()), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.DiasNaoUteis.Should().HaveCount(2);
        resultado.Value.Id.Should().Be(calendario.Id);
        resultado.Value.VersaoDataset.Should().Be("2027.1");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Calendário inexistente retorna NaoEncontrado sem persistir")]
    public async Task Handle_CalendarioInexistente_RetornaNaoEncontrado()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((CalendarioDiasUteis?)null);

        Result<CalendarioDiasUteisDto> resultado = await IncluirDiaNaoUtilCommandHandler.Handle(
            new IncluirDiaNaoUtilCommand(id, ItemValido()), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.NaoEncontrado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Item inválido devolve violação de domínio sem persistir")]
    public async Task Handle_ItemInvalido_RetornaViolacaoDeDominioSemPersistir()
    {
        CalendarioDiasUteis calendario = CalendarioExistente();
        _repository.ObterPorIdAsync(calendario.Id, Arg.Any<CancellationToken>()).Returns(calendario);
        var itemInvalido = new DiaNaoUtilCommandItem("INVALIDO", null, null, null, new DateOnly(2027, 2, 1), "Dia qualquer");

        Result<CalendarioDiasUteisDto> resultado = await IncluirDiaNaoUtilCommandHandler.Handle(
            new IncluirDiaNaoUtilCommand(calendario.Id, itemInvalido), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.AbrangenciaInvalida);
        calendario.DiasNaoUteis.Should().HaveCount(1);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Item duplicado contra o agregado já persistido devolve DataDuplicadaNoDataset sem persistir")]
    public async Task Handle_ItemDuplicadoContraAgregado_RetornaErroSemPersistir()
    {
        CalendarioDiasUteis calendario = CalendarioExistente();
        _repository.ObterPorIdAsync(calendario.Id, Arg.Any<CancellationToken>()).Returns(calendario);
        var itemDuplicado = new DiaNaoUtilCommandItem(
            "NACIONAL", null, null, null, new DateOnly(2027, 1, 1), "Confraternização Universal");

        Result<CalendarioDiasUteisDto> resultado = await IncluirDiaNaoUtilCommandHandler.Handle(
            new IncluirDiaNaoUtilCommand(calendario.Id, itemDuplicado), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.DataDuplicadaNoDataset);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
