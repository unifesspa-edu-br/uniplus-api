namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TermosConsentimento;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class RemoverTermoConsentimentoCommandHandlerTests
{
    private static readonly DateTimeOffset Agora = new(2027, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly ITermoConsentimentoRepository _repository = Substitute.For<ITermoConsentimentoRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    [Fact(DisplayName = "Remove termo sem nenhuma versão promovida")]
    public async Task Handle_SemVersaoPromovida_RemoveEPersiste()
    {
        TermoConsentimento termo = TermoConsentimento.Criar("Termo LGPD", null, null, null).Value!;
        _repository.ObterPorIdAsync(termo.Id, Arg.Any<CancellationToken>()).Returns(termo);

        Result resultado = await RemoverTermoConsentimentoCommandHandler.Handle(
            new RemoverTermoConsentimentoCommand(termo.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        _repository.Received(1).Remover(termo);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Recusa remover termo com versão promovida")]
    public async Task Handle_ComVersaoPromovida_RecusaSemPersistir()
    {
        TermoConsentimento termo = TermoConsentimento.Criar("Termo LGPD", "Texto", "Lei 13.709/2018", null).Value!;
        termo.MarcarRevisado("usuario.revisor", Agora);
        termo.Promover("usuario.revisor", Agora);
        _repository.ObterPorIdAsync(termo.Id, Arg.Any<CancellationToken>()).Returns(termo);

        Result resultado = await RemoverTermoConsentimentoCommandHandler.Handle(
            new RemoverTermoConsentimentoCommand(termo.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TermoConsentimentoErrorCodes.RemocaoBloqueadaComVersaoPromovida);
        _repository.DidNotReceive().Remover(Arg.Any<TermoConsentimento>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Termo inexistente falha sem persistir")]
    public async Task Handle_TermoInexistente_RetornaErroSemPersistir()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((TermoConsentimento?)null);

        Result resultado = await RemoverTermoConsentimentoCommandHandler.Handle(
            new RemoverTermoConsentimentoCommand(id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TermoConsentimentoErrorCodes.NaoEncontrado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
