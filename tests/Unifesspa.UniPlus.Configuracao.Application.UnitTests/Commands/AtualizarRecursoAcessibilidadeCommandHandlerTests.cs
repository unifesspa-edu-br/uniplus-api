namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.RecursosAcessibilidade;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class AtualizarRecursoAcessibilidadeCommandHandlerTests
{
    private readonly IRecursoAcessibilidadeRepository _repository = Substitute.For<IRecursoAcessibilidadeRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static RecursoAcessibilidade Existente(string nome = "Ledor") =>
        RecursoAcessibilidade.Criar(nome, null).Value!;

    private static AtualizarRecursoAcessibilidadeCommand Comando(Guid id, string nome = "Ledor") =>
        new(id, nome, "Descrição atualizada");

    [Fact(DisplayName = "Recurso inexistente retorna NaoEncontrado (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrado()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((RecursoAcessibilidade?)null);

        Result resultado = await AtualizarRecursoAcessibilidadeCommandHandler.Handle(
            Comando(id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(RecursoAcessibilidadeErrorCodes.NaoEncontrado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Editar para um nome que colide com outro recurso vivo retorna conflito (409)")]
    public async Task Handle_NomeColidente_RetornaConflito()
    {
        RecursoAcessibilidade existente = Existente("Ledor");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        // O novo nome "Tempo adicional" já pertence a outro recurso vivo.
        _repository.NomeExisteEntreVivosAsync("Tempo adicional", existente.Id, Arg.Any<CancellationToken>()).Returns(true);

        Result resultado = await AtualizarRecursoAcessibilidadeCommandHandler.Handle(
            Comando(existente.Id, nome: "Tempo adicional"), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(RecursoAcessibilidadeErrorCodes.NomeJaExiste);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Editar para um nome distinto e livre é aceito e persiste")]
    public async Task Handle_NomeDistintoLivre_Aceita()
    {
        RecursoAcessibilidade existente = Existente("Ledor");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        _repository.NomeExisteEntreVivosAsync("Prova ampliada", existente.Id, Arg.Any<CancellationToken>()).Returns(false);

        Result resultado = await AtualizarRecursoAcessibilidadeCommandHandler.Handle(
            Comando(existente.Id, nome: "Prova ampliada"), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.Nome.Should().Be("Prova ampliada");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Editar sem mudar o nome não consulta a unicidade")]
    public async Task Handle_NomeInalterado_NaoChecaUnicidade()
    {
        RecursoAcessibilidade existente = Existente("Ledor");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await AtualizarRecursoAcessibilidadeCommandHandler.Handle(
            Comando(existente.Id, nome: "Ledor"), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        await _repository.DidNotReceive()
            .NomeExisteEntreVivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
