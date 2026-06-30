namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.RecursosAcessibilidade;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class RemoverRecursoAcessibilidadeCommandHandlerTests
{
    private readonly IRecursoAcessibilidadeRepository _repository = Substitute.For<IRecursoAcessibilidadeRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static RecursoAcessibilidade Recurso() =>
        RecursoAcessibilidade.Criar("Ledor", null).Value!;

    [Fact(DisplayName = "Remover um recurso existente faz soft-delete (Remover + Salvar) sem bloqueio")]
    public async Task Handle_RecursoExistente_FazSoftDelete()
    {
        RecursoAcessibilidade recurso = Recurso();
        _repository.ObterPorIdAsync(recurso.Id, Arg.Any<CancellationToken>()).Returns(recurso);

        Result resultado = await RemoverRecursoAcessibilidadeCommandHandler.Handle(
            new RemoverRecursoAcessibilidadeCommand(recurso.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        _repository.Received(1).Remover(recurso);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Remover um recurso inexistente retorna NaoEncontrado (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrado()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((RecursoAcessibilidade?)null);

        Result resultado = await RemoverRecursoAcessibilidadeCommandHandler.Handle(
            new RemoverRecursoAcessibilidadeCommand(id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(RecursoAcessibilidadeErrorCodes.NaoEncontrado);
        _repository.DidNotReceive().Remover(Arg.Any<RecursoAcessibilidade>());
    }
}
