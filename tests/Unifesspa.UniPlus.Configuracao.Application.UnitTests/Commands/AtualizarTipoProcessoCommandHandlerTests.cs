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

public sealed class AtualizarTipoProcessoCommandHandlerTests
{
    private readonly ITipoProcessoRepository _repository = Substitute.For<ITipoProcessoRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static TipoProcesso Existente(string codigo = "CODIGO_EXISTENTE") =>
        TipoProcesso.Criar(codigo, "Nome original", null).Value!;

    [Fact(DisplayName = "Tipo inexistente retorna NaoEncontrado (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrado()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((TipoProcesso?)null);

        Result resultado = await AtualizarTipoProcessoCommandHandler.Handle(
            new AtualizarTipoProcessoCommand(id, "Nome novo"), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoProcessoErrorCodes.NaoEncontrado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Atualização válida persiste e o código permanece imutável")]
    public async Task Handle_Valido_PersisteCodigoImutavel()
    {
        TipoProcesso existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await AtualizarTipoProcessoCommandHandler.Handle(
            new AtualizarTipoProcessoCommand(existente.Id, "Nome atualizado", "Descrição nova"),
            _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.Codigo.Should().Be("CODIGO_EXISTENTE", "o código é imutável — não há campo para alterá-lo");
        existente.Nome.Should().Be("Nome atualizado");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Antes do validator removido, um payload mal formado nunca chegava a
    /// ObterPorIdAsync — validação sempre vencia sobre "não encontrado". Sem o
    /// validator, o handler precisa preservar essa prioridade explicitamente.
    /// </summary>
    [Fact(DisplayName = "Id inexistente com Nome vazio devolve a violação de campo, não NaoEncontrado, sem consultar o repositório")]
    public async Task Handle_IdInexistenteComNomeVazio_RetornaViolacaoDeCampoSemConsultarRepositorio()
    {
        Result resultado = await AtualizarTipoProcessoCommandHandler.Handle(
            new AtualizarTipoProcessoCommand(Guid.CreateVersion7(), ""), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoProcessoErrorCodes.NomeObrigatorio);
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Nome vazio e descrição longa no mesmo payload acumulam as duas violações")]
    public async Task Handle_NomeVazioEDescricaoLonga_AcumulaAsDuasViolacoes()
    {
        Result resultado = await AtualizarTipoProcessoCommandHandler.Handle(
            new AtualizarTipoProcessoCommand(Guid.CreateVersion7(), "", new string('a', 1001)),
            _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Field.Should().Be("nome");
        resultado.Errors[1].Field.Should().Be("descricao");
    }

    [Fact(DisplayName = "DbUpdateConcurrencyException (xmin) no commit descarta o rastreamento antes de devolver 409")]
    public async Task Handle_ConflitoDeConcorrencia_DescartaRastreamentoEDevolveConflito()
    {
        TipoProcesso existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateConcurrencyException("conflito sintético de teste"));

        Result resultado = await AtualizarTipoProcessoCommandHandler.Handle(
            new AtualizarTipoProcessoCommand(existente.Id, "Nome atualizado"), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoProcessoErrorCodes.ConflitoDeConcorrencia);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }
}
