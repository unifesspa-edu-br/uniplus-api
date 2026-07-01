namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.FasesCanonicas;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class AtualizarFaseCanonicaCommandHandlerTests
{
    private readonly IFaseCanonicaRepository _repository = Substitute.For<IFaseCanonicaRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static FaseCanonica Existente(string codigo = "ENSALAMENTO") =>
        FaseCanonica.Criar(codigo, "Ensalamento", null, "CEPS", false, false, null).Value!;

    [Fact(DisplayName = "Fase inexistente retorna NaoEncontrada (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrada()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((FaseCanonica?)null);

        Result resultado = await AtualizarFaseCanonicaCommandHandler.Handle(
            new AtualizarFaseCanonicaCommand(id, Nome: "x", DonoTipico: "CEPS"), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FaseCanonicaErrorCodes.NaoEncontrada);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Atualização válida persiste e o código permanece imutável")]
    public async Task Handle_Valido_PersisteCodigoImutavel()
    {
        FaseCanonica existente = Existente("ENSALAMENTO");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        var comando = new AtualizarFaseCanonicaCommand(
            existente.Id, Nome: "Ensalamento (novo)", DonoTipico: "CRCA");

        Result resultado = await AtualizarFaseCanonicaCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.Codigo.Valor.Should().Be("ENSALAMENTO", "o código é imutável — não há campo para alterá-lo");
        existente.Nome.Should().Be("Ensalamento (novo)");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Coerência revalidada contra o código congelado (agrupar etapas) retorna 422 sem persistir")]
    public async Task Handle_CoerenciaInvalida_Retorna422()
    {
        FaseCanonica existente = Existente("HOMOLOGACAO");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        var comando = new AtualizarFaseCanonicaCommand(
            existente.Id, Nome: "Homologação", DonoTipico: "CEPS", AgrupaEtapas: true);

        Result resultado = await AtualizarFaseCanonicaCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FaseCanonicaErrorCodes.AgrupaEtapasApenasAvaliacao);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
