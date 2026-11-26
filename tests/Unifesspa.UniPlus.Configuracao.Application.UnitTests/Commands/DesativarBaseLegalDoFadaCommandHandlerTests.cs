namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.BaseLegalDoFada;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class DesativarBaseLegalDoFadaCommandHandlerTests
{
    private readonly IBaseLegalDoFadaRepository _repository = Substitute.For<IBaseLegalDoFadaRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static BaseLegalDoFada Existente() =>
        BaseLegalDoFada.Criar(
            "PORTARIA",
            "Portaria Unifesspa nº 2514/2023",
            "Institui inclusão regional",
            [("1504208", "Marabá", "PA")]).Value!;

    [Fact(DisplayName = "Registro existente faz soft-delete (Remover + Salvar) sem bloqueio")]
    public async Task Handle_Existente_FazSoftDelete()
    {
        BaseLegalDoFada existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await DesativarBaseLegalDoFadaCommandHandler.Handle(
            new DesativarBaseLegalDoFadaCommand(existente.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        _repository.Received(1).Remover(existente);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Registro inexistente retorna NaoEncontrado sem remover")]
    public async Task Handle_Inexistente_RetornaNaoEncontrado()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((BaseLegalDoFada?)null);

        Result resultado = await DesativarBaseLegalDoFadaCommandHandler.Handle(
            new DesativarBaseLegalDoFadaCommand(id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(BaseLegalDoFadaErrorCodes.NaoEncontrado);
        _repository.DidNotReceive().Remover(Arg.Any<BaseLegalDoFada>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
