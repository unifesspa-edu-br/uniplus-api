namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TermosConsentimento;
using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CriarTermoConsentimentoCommandHandlerTests
{
    private readonly ITermoConsentimentoRepository _repository = Substitute.For<ITermoConsentimentoRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact(DisplayName = "Cria o termo com rascunho vazio, persiste e retorna o Id")]
    public async Task Handle_RascunhoVazio_CriaEPersiste()
    {
        CriarTermoConsentimentoCommand comando = new("Declaração de veracidade", null, null, null);

        Result<Guid> resultado = await CriarTermoConsentimentoCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBe(Guid.Empty);
        await _repository.Received(1).AdicionarAsync(Arg.Any<TermoConsentimento>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Comando com nome vazio falha sem persistir")]
    public async Task Handle_NomeVazio_RetornaErroSemPersistir()
    {
        CriarTermoConsentimentoCommand comando = new(string.Empty, null, null, null);

        Result<Guid> resultado = await CriarTermoConsentimentoCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TermoConsentimentoErrorCodes.NomeObrigatorio);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<TermoConsentimento>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código já usado por um termo vivo é recusado com CodigoJaExiste, sem persistir")]
    public async Task Handle_CodigoJaExiste_Recusa()
    {
        _repository.CodigoExisteAsync(Arg.Is<CodigoTermoConsentimento>(c => c.Valor == "DECLARACAO_DE_VERACIDADE"), Arg.Any<CancellationToken>())
            .Returns(true);

        Result<Guid> resultado = await CriarTermoConsentimentoCommandHandler.Handle(
            new CriarTermoConsentimentoCommand("Declaração de veracidade", null, null, null), _repository, _unitOfWork, CancellationToken.None);

        resultado.Error!.Code.Should().Be(TermoConsentimentoErrorCodes.CodigoJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<TermoConsentimento>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Corrida decidida pelo índice único do código vira CodigoJaExiste e descarta o rastreamento")]
    public async Task Handle_CorridaNoIndice_TraduzEDescarta()
    {
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateException(
                "violação sintética de teste",
                PostgresExceptionFactory.Create("23505", RestricoesDoBanco.TermoDeConsentimentoCodigoVivo)));

        Result<Guid> resultado = await CriarTermoConsentimentoCommandHandler.Handle(
            new CriarTermoConsentimentoCommand("Declaração de veracidade", null, null, null), _repository, _unitOfWork, CancellationToken.None);

        resultado.Error!.Code.Should().Be(TermoConsentimentoErrorCodes.CodigoJaExiste);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Violação de outra constraint propaga, sem virar CodigoJaExiste")]
    public async Task Handle_OutraConstraint_Propaga()
    {
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateException("outra", PostgresExceptionFactory.Create("23505", "ix_outro")));

        Func<Task> acao = () => CriarTermoConsentimentoCommandHandler.Handle(
            new CriarTermoConsentimentoCommand("Declaração de veracidade", null, null, null), _repository, _unitOfWork, CancellationToken.None);

        await acao.Should().ThrowAsync<DbUpdateException>();
    }
}
