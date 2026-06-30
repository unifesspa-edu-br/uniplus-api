namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDeficiencia;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class AtualizarTipoDeficienciaCommandHandlerTests
{
    private readonly ITipoDeficienciaRepository _repository = Substitute.For<ITipoDeficienciaRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static TipoDeficiencia TipoExistente(string nome = "Visual") =>
        TipoDeficiencia.Criar(nome, null).Value!;

    private static AtualizarTipoDeficienciaCommand Comando(Guid id, string nome = "Visual") =>
        new(id, nome, "Deficiência relacionada à visão");

    [Fact(DisplayName = "Tipo inexistente retorna NaoEncontrado (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrado()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((TipoDeficiencia?)null);

        Result resultado = await AtualizarTipoDeficienciaCommandHandler.Handle(
            Comando(id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.NaoEncontrado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Editar para um nome que colide com outro tipo vivo retorna conflito (409)")]
    public async Task Handle_NomeColidente_RetornaConflito()
    {
        TipoDeficiencia existente = TipoExistente("Visual");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        // O novo nome "Auditiva" já pertence a outro tipo vivo.
        _repository.NomeExisteEntreVivosAsync("Auditiva", existente.Id, Arg.Any<CancellationToken>()).Returns(true);

        Result resultado = await AtualizarTipoDeficienciaCommandHandler.Handle(
            Comando(existente.Id, nome: "Auditiva"), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.NomeJaExiste);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Editar para um nome distinto e livre é aceito e persiste")]
    public async Task Handle_NomeDistintoLivre_Aceita()
    {
        TipoDeficiencia existente = TipoExistente("Visual");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        _repository.NomeExisteEntreVivosAsync("Auditiva", existente.Id, Arg.Any<CancellationToken>()).Returns(false);

        Result resultado = await AtualizarTipoDeficienciaCommandHandler.Handle(
            Comando(existente.Id, nome: "Auditiva"), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.Nome.Should().Be("Auditiva");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Editar sem mudar o nome não consulta a unicidade")]
    public async Task Handle_NomeInalterado_NaoChecaUnicidade()
    {
        TipoDeficiencia existente = TipoExistente("Visual");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await AtualizarTipoDeficienciaCommandHandler.Handle(
            Comando(existente.Id, nome: "Visual"), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        await _repository.DidNotReceive()
            .NomeExisteEntreVivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
