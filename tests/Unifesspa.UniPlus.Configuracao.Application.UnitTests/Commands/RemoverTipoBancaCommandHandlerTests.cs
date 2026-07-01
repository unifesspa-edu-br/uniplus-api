namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposBanca;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class RemoverTipoBancaCommandHandlerTests
{
    private readonly ITipoBancaRepository _repository = Substitute.For<ITipoBancaRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static TipoBanca Banca() =>
        TipoBanca.Criar("BANCA_ENTREVISTA", "Banca de entrevista", null, null).Value!;

    [Fact(DisplayName = "Tipo de banca existente faz soft-delete (Remover + Salvar), nunca bloqueado")]
    public async Task Handle_Existente_FazSoftDelete()
    {
        TipoBanca banca = Banca();
        _repository.ObterPorIdAsync(banca.Id, Arg.Any<CancellationToken>()).Returns(banca);

        Result resultado = await RemoverTipoBancaCommandHandler.Handle(
            new RemoverTipoBancaCommand(banca.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        _repository.Received(1).Remover(banca);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Tipo de banca inexistente retorna NaoEncontrado (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrado()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((TipoBanca?)null);

        Result resultado = await RemoverTipoBancaCommandHandler.Handle(
            new RemoverTipoBancaCommand(id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoBancaErrorCodes.NaoEncontrado);
        _repository.DidNotReceive().Remover(Arg.Any<TipoBanca>());
    }
}
