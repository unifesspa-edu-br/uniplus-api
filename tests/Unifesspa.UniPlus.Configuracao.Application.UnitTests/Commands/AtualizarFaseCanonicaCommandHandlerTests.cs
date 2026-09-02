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
        FaseCanonica.Criar(codigo, "Ensalamento", null, "CEPS", false, false, null, false, false, false, false, "PROPRIA").Value!;

    [Fact(DisplayName = "Fase inexistente retorna NaoEncontrada (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrada()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((FaseCanonica?)null);

        Result resultado = await AtualizarFaseCanonicaCommandHandler.Handle(
            new AtualizarFaseCanonicaCommand(id, Nome: "x", DonoTipico: "CEPS", OrigemData: "PROPRIA"), _repository, _unitOfWork, CancellationToken.None);

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
            existente.Id, Nome: "Ensalamento (novo)", DonoTipico: "CRCA", OrigemData: "PROPRIA");

        Result resultado = await AtualizarFaseCanonicaCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.Codigo.Valor.Should().Be("ENSALAMENTO", "o código é imutável — não há campo para alterá-lo");
        existente.Nome.Should().Be("Ensalamento (novo)");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Coerência dependente do código (agrupar etapas) busca a fase por Id e só então recusa (422) sem persistir")]
    public async Task Handle_CoerenciaInvalida_BuscaPorIdERecusa422SemPersistir()
    {
        FaseCanonica existente = Existente("HOMOLOGACAO");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        var comando = new AtualizarFaseCanonicaCommand(
            existente.Id, Nome: "Homologação", DonoTipico: "CEPS", AgrupaEtapas: true, OrigemData: "PROPRIA");

        Result resultado = await AtualizarFaseCanonicaCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FaseCanonicaErrorCodes.AgrupaEtapasApenasAvaliacao,
            "essa coerência só é decidível depois do fetch, porque depende do código persistido (imutável)");
        await _repository.Received(1).ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Nome ausente (independente do código) retorna a violação sem buscar a fase por Id")]
    public async Task Handle_NomeAusente_RetornaViolacaoSemBuscarPorId()
    {
        var comando = new AtualizarFaseCanonicaCommand(
            Guid.CreateVersion7(), Nome: "", DonoTipico: "CEPS", OrigemData: "PROPRIA");

        Result resultado = await AtualizarFaseCanonicaCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FaseCanonicaErrorCodes.NomeObrigatorio);
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
