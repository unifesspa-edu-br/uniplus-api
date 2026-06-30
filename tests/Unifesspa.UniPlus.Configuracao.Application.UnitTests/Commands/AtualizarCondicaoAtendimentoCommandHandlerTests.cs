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

public sealed class AtualizarCondicaoAtendimentoCommandHandlerTests
{
    private readonly ICondicaoAtendimentoRepository _repository = Substitute.For<ICondicaoAtendimentoRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static CondicaoAtendimentoEspecializado Existente(string codigo = "LACTANTE") =>
        CondicaoAtendimentoEspecializado.Criar(codigo, "Lactante", null).Value!;

    private static AtualizarCondicaoAtendimentoCommand Comando(Guid id, string codigo = "LACTANTE") =>
        new(id, codigo, "Lactante", "Atendimento ampliado");

    [Fact(DisplayName = "Condição inexistente retorna NaoEncontrada (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrada()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((CondicaoAtendimentoEspecializado?)null);

        Result resultado = await AtualizarCondicaoAtendimentoCommandHandler.Handle(
            Comando(id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CondicaoAtendimentoErrorCodes.NaoEncontrada);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Editar para um código que colide com outra condição viva retorna conflito (409)")]
    public async Task Handle_CodigoColidente_RetornaConflito()
    {
        CondicaoAtendimentoEspecializado existente = Existente("LACTANTE");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        _repository.CodigoExisteEntreVivosAsync("DISLEXIA", existente.Id, Arg.Any<CancellationToken>()).Returns(true);

        Result resultado = await AtualizarCondicaoAtendimentoCommandHandler.Handle(
            Comando(existente.Id, codigo: "DISLEXIA"), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CondicaoAtendimentoErrorCodes.CodigoJaExiste);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Editar para um código distinto e livre é aceito e persiste")]
    public async Task Handle_CodigoDistintoLivre_Aceita()
    {
        CondicaoAtendimentoEspecializado existente = Existente("LACTANTE");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        _repository.CodigoExisteEntreVivosAsync("DISLEXIA", existente.Id, Arg.Any<CancellationToken>()).Returns(false);

        Result resultado = await AtualizarCondicaoAtendimentoCommandHandler.Handle(
            Comando(existente.Id, codigo: "DISLEXIA"), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.Codigo.Valor.Should().Be("DISLEXIA");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Editar sem mudar o código não consulta a unicidade")]
    public async Task Handle_CodigoInalterado_NaoChecaUnicidade()
    {
        CondicaoAtendimentoEspecializado existente = Existente("LACTANTE");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await AtualizarCondicaoAtendimentoCommandHandler.Handle(
            Comando(existente.Id, codigo: "LACTANTE"), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        await _repository.DidNotReceive()
            .CodigoExisteEntreVivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Renomear o código reservado PCD é bloqueado (CodigoProtegidoNaoEditavel) sem persistir")]
    public async Task Handle_RenomearPcd_Bloqueia()
    {
        CondicaoAtendimentoEspecializado pcd = Existente(CodigoCondicao.Pcd);
        _repository.ObterPorIdAsync(pcd.Id, Arg.Any<CancellationToken>()).Returns(pcd);
        _repository.CodigoExisteEntreVivosAsync("LACTANTE", pcd.Id, Arg.Any<CancellationToken>()).Returns(false);

        Result resultado = await AtualizarCondicaoAtendimentoCommandHandler.Handle(
            new AtualizarCondicaoAtendimentoCommand(pcd.Id, "LACTANTE", "Lactante", null),
            _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CondicaoAtendimentoErrorCodes.CodigoProtegidoNaoEditavel);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
