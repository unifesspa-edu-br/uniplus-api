namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposBanca;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class AtualizarTipoBancaCommandHandlerTests
{
    private readonly ITipoBancaRepository _repository = Substitute.For<ITipoBancaRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static TipoBanca Existente(string codigo = "BANCA_ANALISE_RECURSOS") =>
        TipoBanca.Criar(codigo, "Banca de análise de recursos", null, null).Value!;

    [Fact(DisplayName = "Tipo de banca inexistente retorna NaoEncontrado (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrado()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((TipoBanca?)null);

        Result resultado = await AtualizarTipoBancaCommandHandler.Handle(
            new AtualizarTipoBancaCommand(id, Nome: "x"), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoBancaErrorCodes.NaoEncontrado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Atualização válida persiste e o código permanece imutável")]
    public async Task Handle_Valido_PersisteCodigoImutavel()
    {
        TipoBanca existente = Existente("BANCA_ANALISE_RECURSOS");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        var comando = new AtualizarTipoBancaCommand(existente.Id, Nome: "Banca de recursos (novo)", FaseTipica: "Recursos");

        Result resultado = await AtualizarTipoBancaCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.Codigo.Valor.Should().Be("BANCA_ANALISE_RECURSOS", "o código é imutável — não há campo para alterá-lo");
        existente.Nome.Should().Be("Banca de recursos (novo)");
        existente.FaseTipica.Should().Be("Recursos");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
