namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposProcesso;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class ReativarTipoProcessoCommandHandlerTests
{
    private readonly ITipoProcessoRepository _repository = Substitute.For<ITipoProcessoRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static TipoProcesso Desativado()
    {
        TipoProcesso tipo = TipoProcesso.Criar("CODIGO_EXISTENTE", "Nome original", null).Value!;
        tipo.Desativar();
        return tipo;
    }

    [Fact(DisplayName = "Tipo inexistente retorna NaoEncontrado (404) e não persiste")]
    public async Task Handle_Inexistente_RetornaNaoEncontrado()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((TipoProcesso?)null);

        Result resultado = await ReativarTipoProcessoCommandHandler.Handle(
            new ReativarTipoProcessoCommand(id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoProcessoErrorCodes.NaoEncontrado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Tipo desativado é reativado e a alteração é persistida")]
    public async Task Handle_Desativado_ReativaEPersiste()
    {
        TipoProcesso existente = Desativado();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await ReativarTipoProcessoCommandHandler.Handle(
            new ReativarTipoProcessoCommand(existente.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.Ativo.Should().BeTrue();
        existente.Codigo.Should().Be("CODIGO_EXISTENTE");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Tipo já ativo retorna JaAtivo (422) e não persiste")]
    public async Task Handle_JaAtivo_RetornaJaAtivoSemPersistir()
    {
        TipoProcesso existente = TipoProcesso.Criar("CODIGO_EXISTENTE", "Nome original", null).Value!;
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await ReativarTipoProcessoCommandHandler.Handle(
            new ReativarTipoProcessoCommand(existente.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoProcessoErrorCodes.JaAtivo);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "DbUpdateConcurrencyException (xmin) no commit descarta o rastreamento antes de devolver 409")]
    public async Task Handle_ConflitoDeConcorrencia_DescartaRastreamentoEDevolveConflito()
    {
        TipoProcesso existente = Desativado();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateConcurrencyException("conflito sintético de teste"));

        Result resultado = await ReativarTipoProcessoCommandHandler.Handle(
            new ReativarTipoProcessoCommand(existente.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoProcessoErrorCodes.ConflitoDeConcorrencia);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }
}
