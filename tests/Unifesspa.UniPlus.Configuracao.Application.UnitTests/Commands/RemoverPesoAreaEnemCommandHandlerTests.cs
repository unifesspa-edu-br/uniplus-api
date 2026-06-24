namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.PesosAreaEnem;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class RemoverPesoAreaEnemCommandHandlerTests
{
    private readonly IPesoAreaEnemRepository _repository = Substitute.For<IPesoAreaEnemRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact(DisplayName = "Linha inexistente retorna NaoEncontrado")]
    public async Task Handle_NaoEncontrado_RetornaErro()
    {
        Guid id = Guid.NewGuid();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((PesoAreaEnem?)null);

        Result resultado = await RemoverPesoAreaEnemCommandHandler.Handle(
            new RemoverPesoAreaEnemCommand(id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(PesoAreaEnemErrorCodes.NaoEncontrado);
        _repository.DidNotReceive().Remover(Arg.Any<PesoAreaEnem>());
    }

    [Fact(DisplayName = "Sem FK de entrada: remoção é soft-delete e nunca é bloqueada (CA-05)")]
    public async Task Handle_Existente_RemoveESoftDelete()
    {
        PesoAreaEnem existente = PesoAreaEnem
            .Criar("Res. 805/2024", GrupoCurso.Tecnologica, 1.50m, 1.00m, 1.00m, 1.00m, 2.00m, 400m, "Res. 805/2024 Anexo I")
            .Value!;
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await RemoverPesoAreaEnemCommandHandler.Handle(
            new RemoverPesoAreaEnemCommand(existente.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        _repository.Received(1).Remover(existente);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
