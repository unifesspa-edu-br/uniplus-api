namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ReferenciasReservaDemografica;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class AtualizarReferenciaReservaDemograficaCommandHandlerTests
{
    private const string BaseLegal = "Lei 12.711/2012, art. 10, III";

    private readonly IReferenciaReservaDemograficaRepository _repository =
        Substitute.For<IReferenciaReservaDemograficaRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static ReferenciaReservaDemografica Existente() =>
        ReferenciaReservaDemografica.Criar("2022", 78.50m, 1.20m, 8.40m, BaseLegal).Value!;

    [Fact(DisplayName = "Referência inexistente retorna NaoEncontrada")]
    public async Task Handle_NaoEncontrada_RetornaErro()
    {
        Guid id = Guid.NewGuid();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((ReferenciaReservaDemografica?)null);

        Result resultado = await AtualizarReferenciaReservaDemograficaCommandHandler.Handle(
            new AtualizarReferenciaReservaDemograficaCommand(id, "2022", 79m, 1.3m, 8.5m, BaseLegal),
            _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ReferenciaReservaDemograficaErrorCodes.NaoEncontrada);
    }

    [Fact(DisplayName = "Edição válida persiste e mantém sucesso")]
    public async Task Handle_DadosValidos_Persiste()
    {
        ReferenciaReservaDemografica existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await AtualizarReferenciaReservaDemograficaCommandHandler.Handle(
            new AtualizarReferenciaReservaDemograficaCommand(existente.Id, "2022", 79m, 1.3m, 8.5m, BaseLegal),
            _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.PpiPercentual.Valor.Should().Be(79m);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Novo Censo já usado por outra referência viva retorna conflito")]
    public async Task Handle_CensoDuplicado_RetornaConflito()
    {
        ReferenciaReservaDemografica existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        _repository.CensoExisteEntreLivosAsync("2010", existente.Id, Arg.Any<CancellationToken>()).Returns(true);

        Result resultado = await AtualizarReferenciaReservaDemograficaCommandHandler.Handle(
            new AtualizarReferenciaReservaDemograficaCommand(existente.Id, "2010", 79m, 1.3m, 8.5m, BaseLegal),
            _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ReferenciaReservaDemograficaErrorCodes.CensoJaExiste);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
