namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.FasesCanonicas;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class RemoverFaseCanonicaCommandHandlerTests
{
    private readonly IFaseCanonicaRepository _repository = Substitute.For<IFaseCanonicaRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static FaseCanonica Fase() =>
        FaseCanonica.Criar("INSCRICAO", "Inscrição", null, "CEPS", false, false, null).Value!;

    [Fact(DisplayName = "Fase existente faz soft-delete (Remover + Salvar), nunca bloqueada")]
    public async Task Handle_Existente_FazSoftDelete()
    {
        FaseCanonica fase = Fase();
        _repository.ObterPorIdAsync(fase.Id, Arg.Any<CancellationToken>()).Returns(fase);

        Result resultado = await RemoverFaseCanonicaCommandHandler.Handle(
            new RemoverFaseCanonicaCommand(fase.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        _repository.Received(1).Remover(fase);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Fase inexistente retorna NaoEncontrada (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrada()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((FaseCanonica?)null);

        Result resultado = await RemoverFaseCanonicaCommandHandler.Handle(
            new RemoverFaseCanonicaCommand(id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FaseCanonicaErrorCodes.NaoEncontrada);
        _repository.DidNotReceive().Remover(Arg.Any<FaseCanonica>());
    }
}
