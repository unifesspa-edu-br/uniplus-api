namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TermosConsentimento;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class EditarRascunhoTermoConsentimentoCommandHandlerTests
{
    private readonly ITermoConsentimentoRepository _repository = Substitute.For<ITermoConsentimentoRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static TermoConsentimento TermoExistente() =>
        TermoConsentimento.Criar("Termo LGPD", "Texto", "Lei 13.709/2018", null).Value!;

    [Fact(DisplayName = "Edita o rascunho de um termo existente e persiste")]
    public async Task Handle_TermoExistente_EditaEPersiste()
    {
        TermoConsentimento termo = TermoExistente();
        _repository.ObterPorIdAsync(termo.Id, Arg.Any<CancellationToken>()).Returns(termo);

        EditarRascunhoTermoConsentimentoCommand comando = new(
            termo.Id, "Novo texto", "Nova base legal", "REGISTRO_DIGITAL_SEM_LOG_IP");

        Result resultado = await EditarRascunhoTermoConsentimentoCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        termo.TextoRascunho.Should().Be("Novo texto");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Conflito de concorrência (xmin) descarta o rastreamento antes de devolver 409")]
    public async Task Handle_ConcorrenciaOtimista_DescartaRastreamentoERetornaConflito()
    {
        TermoConsentimento termo = TermoExistente();
        _repository.ObterPorIdAsync(termo.Id, Arg.Any<CancellationToken>()).Returns(termo);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateConcurrencyException("conflito sintético de teste"));

        EditarRascunhoTermoConsentimentoCommand comando = new(termo.Id, "Texto", "Base legal", null);

        Result resultado = await EditarRascunhoTermoConsentimentoCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TermoConsentimentoErrorCodes.ConflitoDeConcorrencia);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Termo inexistente falha sem persistir")]
    public async Task Handle_TermoInexistente_RetornaErroSemPersistir()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((TermoConsentimento?)null);

        EditarRascunhoTermoConsentimentoCommand comando = new(id, "Texto", "Base legal", null);

        Result resultado = await EditarRascunhoTermoConsentimentoCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TermoConsentimentoErrorCodes.NaoEncontrado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Forma de aceite inválida falha sem persistir")]
    public async Task Handle_FormaAceiteInvalida_RetornaErroSemPersistir()
    {
        TermoConsentimento termo = TermoExistente();
        _repository.ObterPorIdAsync(termo.Id, Arg.Any<CancellationToken>()).Returns(termo);

        EditarRascunhoTermoConsentimentoCommand comando = new(termo.Id, "Texto", "Base legal", "FORMA_INEXISTENTE");

        Result resultado = await EditarRascunhoTermoConsentimentoCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TermoConsentimentoErrorCodes.FormaAceiteInvalida);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Campo inválido no payload propaga o erro sem buscar o termo por Id — validação vence 404")]
    public async Task Handle_CampoInvalido_RetornaErroSemBuscarPorId()
    {
        EditarRascunhoTermoConsentimentoCommand comando = new(
            Guid.CreateVersion7(), "Texto", "Base legal", "FORMA_INEXISTENTE");

        Result resultado = await EditarRascunhoTermoConsentimentoCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TermoConsentimentoErrorCodes.FormaAceiteInvalida);
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
