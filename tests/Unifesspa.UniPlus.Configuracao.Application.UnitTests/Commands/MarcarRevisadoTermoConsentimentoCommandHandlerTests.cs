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

public sealed class MarcarRevisadoTermoConsentimentoCommandHandlerTests
{
    private static readonly DateTimeOffset Agora = new(2027, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly ITermoConsentimentoRepository _repository = Substitute.For<ITermoConsentimentoRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static TermoConsentimento TermoRevisavel() =>
        TermoConsentimento.Criar("Termo LGPD", "Texto", "Lei 13.709/2018", null).Value!;

    private static IUserContext UsuarioAutenticado()
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.UserId.Returns("usuario.revisor");
        return contexto;
    }

    [Fact(DisplayName = "Marca o rascunho como revisado gravando o ator resolvido via IUserContext")]
    public async Task Handle_RascunhoCompleto_MarcaRevisadoComAtorDoContexto()
    {
        TermoConsentimento termo = TermoRevisavel();
        _repository.ObterPorIdAsync(termo.Id, Arg.Any<CancellationToken>()).Returns(termo);

        Result resultado = await MarcarRevisadoTermoConsentimentoCommandHandler.Handle(
            new MarcarRevisadoTermoConsentimentoCommand(termo.Id),
            _repository, _unitOfWork, UsuarioAutenticado(), new RelogioFixo(Agora), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        termo.Revisado.Should().BeTrue();
        termo.RevisadoPor.Should().Be("usuario.revisor");
        termo.RevisadoEm.Should().Be(Agora);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Conflito de concorrência (xmin) descarta o rastreamento antes de devolver 409")]
    public async Task Handle_ConcorrenciaOtimista_DescartaRastreamentoERetornaConflito()
    {
        TermoConsentimento termo = TermoRevisavel();
        _repository.ObterPorIdAsync(termo.Id, Arg.Any<CancellationToken>()).Returns(termo);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateConcurrencyException("conflito sintético de teste"));

        Result resultado = await MarcarRevisadoTermoConsentimentoCommandHandler.Handle(
            new MarcarRevisadoTermoConsentimentoCommand(termo.Id),
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

        Result resultado = await MarcarRevisadoTermoConsentimentoCommandHandler.Handle(
            new MarcarRevisadoTermoConsentimentoCommand(id),
            _repository, _unitOfWork, UsuarioAutenticado(), new RelogioFixo(Agora), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TermoConsentimentoErrorCodes.NaoEncontrado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Rascunho sem texto falha sem persistir")]
    public async Task Handle_RascunhoSemTexto_RetornaErroSemPersistir()
    {
        TermoConsentimento termo = TermoConsentimento.Criar("Termo LGPD", null, "Lei 13.709/2018", null).Value!;
        _repository.ObterPorIdAsync(termo.Id, Arg.Any<CancellationToken>()).Returns(termo);

        Result resultado = await MarcarRevisadoTermoConsentimentoCommandHandler.Handle(
            new MarcarRevisadoTermoConsentimentoCommand(termo.Id),
            _repository, _unitOfWork, UsuarioAutenticado(), new RelogioFixo(Agora), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TermoConsentimentoErrorCodes.RevisaoSemTexto);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private sealed class RelogioFixo(DateTimeOffset instante) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instante;
    }
}
