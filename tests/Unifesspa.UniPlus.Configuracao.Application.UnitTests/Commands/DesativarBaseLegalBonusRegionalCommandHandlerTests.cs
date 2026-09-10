namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.BaseLegalBonusRegional;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class DesativarBaseLegalBonusRegionalCommandHandlerTests
{
    private readonly IBaseLegalBonusRegionalRepository _repository = Substitute.For<IBaseLegalBonusRegionalRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static BaseLegalBonusRegional Existente() =>
        BaseLegalBonusRegional.Criar(
            "PORTARIA",
            "Portaria Unifesspa nº 2514/2023",
            "Institui inclusão regional",
            [("1504208", "Marabá", "PA")]).Value!;

    [Fact(DisplayName = "Registro existente faz soft-delete (Remover + Salvar) sem bloqueio")]
    public async Task Handle_Existente_FazSoftDelete()
    {
        BaseLegalBonusRegional existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await DesativarBaseLegalBonusRegionalCommandHandler.Handle(
            new DesativarBaseLegalBonusRegionalCommand(existente.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        _repository.Received(1).Remover(existente);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Registro inexistente retorna NaoEncontrado sem remover")]
    public async Task Handle_Inexistente_RetornaNaoEncontrado()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((BaseLegalBonusRegional?)null);

        Result resultado = await DesativarBaseLegalBonusRegionalCommandHandler.Handle(
            new DesativarBaseLegalBonusRegionalCommand(id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(BaseLegalBonusRegionalErrorCodes.NaoEncontrado);
        _repository.DidNotReceive().Remover(Arg.Any<BaseLegalBonusRegional>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
