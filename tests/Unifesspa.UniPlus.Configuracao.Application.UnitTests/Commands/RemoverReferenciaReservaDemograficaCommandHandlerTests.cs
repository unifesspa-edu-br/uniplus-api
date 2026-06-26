namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ReferenciasReservaDemografica;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class RemoverReferenciaReservaDemograficaCommandHandlerTests
{
    private readonly IReferenciaReservaDemograficaRepository _repository =
        Substitute.For<IReferenciaReservaDemograficaRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    [Fact(DisplayName = "Referência inexistente retorna NaoEncontrada")]
    public async Task Handle_NaoEncontrada_RetornaErro()
    {
        Guid id = Guid.NewGuid();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((ReferenciaReservaDemografica?)null);

        Result resultado = await RemoverReferenciaReservaDemograficaCommandHandler.Handle(
            new RemoverReferenciaReservaDemograficaCommand(id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ReferenciaReservaDemograficaErrorCodes.NaoEncontrada);
        _repository.DidNotReceive().Remover(Arg.Any<ReferenciaReservaDemografica>());
    }

    [Fact(DisplayName = "Cadastro flat: remoção é soft-delete e nunca é bloqueada (CA-05)")]
    public async Task Handle_Existente_RemoveESoftDelete()
    {
        ReferenciaReservaDemografica existente =
            ReferenciaReservaDemografica.Criar("2022", 78.50m, 1.20m, 8.40m, "Lei 12.711/2012").Value!;
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await RemoverReferenciaReservaDemograficaCommandHandler.Handle(
            new RemoverReferenciaReservaDemograficaCommand(existente.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        _repository.Received(1).Remover(existente);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
