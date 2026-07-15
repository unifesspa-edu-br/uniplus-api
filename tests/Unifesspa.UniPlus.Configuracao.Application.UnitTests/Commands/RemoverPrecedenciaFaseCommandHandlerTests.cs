namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.PrecedenciasFase;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class RemoverPrecedenciaFaseCommandHandlerTests
{
    private readonly IPrecedenciaFaseRepository _repository = Substitute.For<IPrecedenciaFaseRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static PrecedenciaFase Aresta() =>
        PrecedenciaFase.Criar("INSCRICAO", "HOMOLOGACAO", false, []).Value!;

    [Fact(DisplayName = "Aresta existente faz soft-delete (Remover + Salvar), nunca bloqueada")]
    public async Task Handle_Existente_FazSoftDelete()
    {
        PrecedenciaFase aresta = Aresta();
        _repository.ObterPorIdAsync(aresta.Id, Arg.Any<CancellationToken>()).Returns(aresta);

        Result resultado = await RemoverPrecedenciaFaseCommandHandler.Handle(
            new RemoverPrecedenciaFaseCommand(aresta.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        _repository.Received(1).Remover(aresta);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Aresta inexistente retorna NaoEncontrada (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrada()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((PrecedenciaFase?)null);

        Result resultado = await RemoverPrecedenciaFaseCommandHandler.Handle(
            new RemoverPrecedenciaFaseCommand(id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(PrecedenciaFaseErrorCodes.NaoEncontrada);
        _repository.DidNotReceive().Remover(Arg.Any<PrecedenciaFase>());
    }
}
