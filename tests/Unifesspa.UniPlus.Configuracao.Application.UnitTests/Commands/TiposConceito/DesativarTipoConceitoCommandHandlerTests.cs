namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.TiposConceito;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposConceito;
using Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class DesativarTipoConceitoCommandHandlerTests
{
    private readonly ITipoConceitoRepository _repository = Substitute.For<ITipoConceitoRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private Task<Result> Desativar(Guid id) =>
        DesativarTipoConceitoCommandHandler.Handle(new DesativarTipoConceitoCommand(id), _repository, _unitOfWork, CancellationToken.None);

    [Fact(DisplayName = "Desativa o tipo e persiste")]
    public async Task Handle_TipoAtivo_DesativaEPersiste()
    {
        TipoConceito tipo = TiposConceitoDeTeste.Numerico();
        _repository.ObterPorIdAsync(tipo.Id, Arg.Any<CancellationToken>()).Returns(tipo);

        Result resultado = await Desativar(tipo.Id);

        resultado.IsSuccess.Should().BeTrue();
        tipo.Ativo.Should().BeFalse();
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Desativar de novo um tipo desativado continua sendo sucesso")]
    public async Task Handle_TipoJaDesativado_Sucesso()
    {
        TipoConceito tipo = TiposConceitoDeTeste.Numerico();
        tipo.Desativar();
        _repository.ObterPorIdAsync(tipo.Id, Arg.Any<CancellationToken>()).Returns(tipo);

        Result resultado = await Desativar(tipo.Id);

        resultado.IsSuccess.Should().BeTrue();
        tipo.Ativo.Should().BeFalse();
    }

    [Fact(DisplayName = "Tipo inexistente retorna não encontrado sem persistir")]
    public async Task Handle_Inexistente_RetornaNaoEncontrado()
    {
        Result resultado = await Desativar(Guid.NewGuid());

        resultado.Error!.Code.Should().Be(TipoConceitoErrorCodes.NaoEncontrado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
