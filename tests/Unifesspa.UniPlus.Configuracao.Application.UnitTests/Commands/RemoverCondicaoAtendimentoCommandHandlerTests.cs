namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.CondicoesAtendimento;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class RemoverCondicaoAtendimentoCommandHandlerTests
{
    private readonly ICondicaoAtendimentoRepository _repository = Substitute.For<ICondicaoAtendimentoRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static CondicaoAtendimentoEspecializado Condicao(string codigo = "DISLEXIA") =>
        CondicaoAtendimentoEspecializado.Criar(codigo, "Dislexia", null).Value!;

    [Fact(DisplayName = "Remover uma condição não reservada faz soft-delete (Remover + Salvar)")]
    public async Task Handle_CondicaoExistente_FazSoftDelete()
    {
        CondicaoAtendimentoEspecializado condicao = Condicao();
        _repository.ObterPorIdAsync(condicao.Id, Arg.Any<CancellationToken>()).Returns(condicao);

        Result resultado = await RemoverCondicaoAtendimentoCommandHandler.Handle(
            new RemoverCondicaoAtendimentoCommand(condicao.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        _repository.Received(1).Remover(condicao);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Remover o código reservado PCD é bloqueado (RemocaoBloqueadaCodigoProtegido) sem persistir")]
    public async Task Handle_Pcd_Bloqueia()
    {
        CondicaoAtendimentoEspecializado pcd = Condicao(CodigoCondicao.Pcd);
        _repository.ObterPorIdAsync(pcd.Id, Arg.Any<CancellationToken>()).Returns(pcd);

        Result resultado = await RemoverCondicaoAtendimentoCommandHandler.Handle(
            new RemoverCondicaoAtendimentoCommand(pcd.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CondicaoAtendimentoErrorCodes.RemocaoBloqueadaCodigoProtegido);
        _repository.DidNotReceive().Remover(Arg.Any<CondicaoAtendimentoEspecializado>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Remover uma condição inexistente retorna NaoEncontrada (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrada()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((CondicaoAtendimentoEspecializado?)null);

        Result resultado = await RemoverCondicaoAtendimentoCommandHandler.Handle(
            new RemoverCondicaoAtendimentoCommand(id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CondicaoAtendimentoErrorCodes.NaoEncontrada);
        _repository.DidNotReceive().Remover(Arg.Any<CondicaoAtendimentoEspecializado>());
    }
}
