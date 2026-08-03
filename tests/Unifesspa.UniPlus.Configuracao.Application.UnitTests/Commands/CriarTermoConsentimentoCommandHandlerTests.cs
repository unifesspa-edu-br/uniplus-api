namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TermosConsentimento;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CriarTermoConsentimentoCommandHandlerTests
{
    private readonly ITermoConsentimentoRepository _repository = Substitute.For<ITermoConsentimentoRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

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
}
