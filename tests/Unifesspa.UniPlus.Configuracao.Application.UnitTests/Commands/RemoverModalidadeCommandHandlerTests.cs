namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Modalidades;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class RemoverModalidadeCommandHandlerTests
{
    private readonly IModalidadeRepository _repository = Substitute.For<IModalidadeRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static Modalidade Modalidade() =>
        Domain.Entities.Modalidade.Criar("AC", null, "AMPLA", "RESIDUAL_DO_VO", null, null, null, null, null, null, null, null)
            .Value!;

    [Fact(DisplayName = "Modalidade referenciada por outra viva bloqueia a remoção (409)")]
    public async Task Handle_Referenciada_RetornaConflito()
    {
        Modalidade modalidade = Modalidade();
        _repository.ObterPorIdAsync(modalidade.Id, Arg.Any<CancellationToken>()).Returns(modalidade);
        _repository.EhReferenciadaPorOutraModalidadeVivaAsync("AC", modalidade.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        Result resultado = await RemoverModalidadeCommandHandler.Handle(
            new RemoverModalidadeCommand(modalidade.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.RemocaoBloqueadaPorReferencia);
        _repository.DidNotReceive().Remover(Arg.Any<Modalidade>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Modalidade livre de referências faz soft-delete (Remover + Salvar)")]
    public async Task Handle_Livre_FazSoftDelete()
    {
        Modalidade modalidade = Modalidade();
        _repository.ObterPorIdAsync(modalidade.Id, Arg.Any<CancellationToken>()).Returns(modalidade);
        _repository.EhReferenciadaPorOutraModalidadeVivaAsync("AC", modalidade.Id, Arg.Any<CancellationToken>())
            .Returns(false);

        Result resultado = await RemoverModalidadeCommandHandler.Handle(
            new RemoverModalidadeCommand(modalidade.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        _repository.Received(1).Remover(modalidade);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Modalidade inexistente retorna NaoEncontrada (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrada()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((Modalidade?)null);

        Result resultado = await RemoverModalidadeCommandHandler.Handle(
            new RemoverModalidadeCommand(id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.NaoEncontrada);
        _repository.DidNotReceive().Remover(Arg.Any<Modalidade>());
    }
}
