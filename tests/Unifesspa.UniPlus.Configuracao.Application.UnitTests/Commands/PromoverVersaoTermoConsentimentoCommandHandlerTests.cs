namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TermosConsentimento;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class PromoverVersaoTermoConsentimentoCommandHandlerTests
{
    private static readonly DateTimeOffset Agora = new(2027, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly ITermoConsentimentoRepository _repository = Substitute.For<ITermoConsentimentoRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static IUserContext UsuarioAutenticado()
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.UserId.Returns("usuario.revisor");
        return contexto;
    }

    private static TermoConsentimento TermoRevisado()
    {
        TermoConsentimento termo = TermoConsentimento.Criar("Termo LGPD", "Texto", "Lei 13.709/2018", null).Value!;
        termo.MarcarRevisado("usuario.revisor", Agora);
        return termo;
    }

    [Fact(DisplayName = "Promove o rascunho revisado, cria versão e persiste")]
    public async Task Handle_RascunhoRevisado_PromoveEPersiste()
    {
        TermoConsentimento termo = TermoRevisado();
        _repository.ObterPorIdAsync(termo.Id, Arg.Any<CancellationToken>()).Returns(termo);

        Result resultado = await PromoverVersaoTermoConsentimentoCommandHandler.Handle(
            new PromoverVersaoTermoConsentimentoCommand(termo.Id),
            _repository, _unitOfWork, UsuarioAutenticado(), new RelogioFixo(Agora), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        termo.Versoes.Should().HaveCount(1);
        termo.Versoes[0].PromovidaPor.Should().Be("usuario.revisor");
        await _repository.Received(1).AdicionarVersaoAsync(
            termo, Arg.Is<TermoConsentimentoVersao>(v => v.PromovidaPor == "usuario.revisor"), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Conflito de concorrência (xmin) no commit vira ConflitoDeConcorrencia")]
    public async Task Handle_ConcorrenciaOtimista_RetornaConflitoDeConcorrencia()
    {
        TermoConsentimento termo = TermoRevisado();
        _repository.ObterPorIdAsync(termo.Id, Arg.Any<CancellationToken>()).Returns(termo);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateConcurrencyException("conflito sintético de teste"));

        Result resultado = await PromoverVersaoTermoConsentimentoCommandHandler.Handle(
            new PromoverVersaoTermoConsentimentoCommand(termo.Id),
            _repository, _unitOfWork, UsuarioAutenticado(), new RelogioFixo(Agora), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TermoConsentimentoErrorCodes.ConflitoDeConcorrencia);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Termo inexistente falha sem persistir")]
    public async Task Handle_TermoInexistente_RetornaErroSemPersistir()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((TermoConsentimento?)null);

        Result resultado = await PromoverVersaoTermoConsentimentoCommandHandler.Handle(
            new PromoverVersaoTermoConsentimentoCommand(id),
            _repository, _unitOfWork, UsuarioAutenticado(), new RelogioFixo(Agora), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TermoConsentimentoErrorCodes.NaoEncontrado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Rascunho não revisado falha sem persistir")]
    public async Task Handle_RascunhoNaoRevisado_RetornaErroSemPersistir()
    {
        TermoConsentimento termo = TermoConsentimento.Criar("Termo LGPD", "Texto", "Lei 13.709/2018", null).Value!;
        _repository.ObterPorIdAsync(termo.Id, Arg.Any<CancellationToken>()).Returns(termo);

        Result resultado = await PromoverVersaoTermoConsentimentoCommandHandler.Handle(
            new PromoverVersaoTermoConsentimentoCommand(termo.Id),
            _repository, _unitOfWork, UsuarioAutenticado(), new RelogioFixo(Agora), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TermoConsentimentoErrorCodes.PromocaoSemRevisao);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private sealed class RelogioFixo(DateTimeOffset instante) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instante;
    }
}
