namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.PrecedenciasFase;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class AtualizarPrecedenciaFaseCommandHandlerTests
{
    private readonly IPrecedenciaFaseRepository _repository = Substitute.For<IPrecedenciaFaseRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    [Fact(DisplayName = "Aresta inexistente retorna NaoEncontrada (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrada()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((PrecedenciaFase?)null);

        Result resultado = await AtualizarPrecedenciaFaseCommandHandler.Handle(
            new AtualizarPrecedenciaFaseCommand(id, true), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(PrecedenciaFaseErrorCodes.NaoEncontrada);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Atualização válida troca PermiteSobreposicao mantendo o par imutável")]
    public async Task Handle_Valido_PersisteParImutavel()
    {
        PrecedenciaFase existente = PrecedenciaFase.Criar("INSCRICAO", "HOMOLOGACAO", false, []).Value!;
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await AtualizarPrecedenciaFaseCommandHandler.Handle(
            new AtualizarPrecedenciaFaseCommand(existente.Id, true), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.PermiteSobreposicao.Should().BeTrue();
        existente.AntecessoraCodigo.Should().Be("INSCRICAO");
        existente.SucessoraCodigo.Should().Be("HOMOLOGACAO");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
