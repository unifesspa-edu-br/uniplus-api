namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.Commands;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.Abstractions;
using Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class RemoverTipoAtoPublicadoCommandHandlerTests
{
    private readonly ITipoAtoPublicadoRepository _repository = Substitute.For<ITipoAtoPublicadoRepository>();
    private readonly IPublicacoesUnitOfWork _unitOfWork = Substitute.For<IPublicacoesUnitOfWork>();

    [Fact(DisplayName = "Recusa quando o tipo de ato não existe")]
    public async Task Handle_NaoEncontrado_Falha()
    {
        _repository.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((TipoAtoPublicado?)null);

        Result resultado = await RemoverTipoAtoPublicadoCommandHandler.Handle(
            new RemoverTipoAtoPublicadoCommand(Guid.NewGuid()), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoAtoPublicadoErrorCodes.NaoEncontrado);
        _repository.DidNotReceive().Remover(Arg.Any<TipoAtoPublicado>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Remove e commita — a remoção nunca é bloqueada")]
    public async Task Handle_Encontrado_Remove()
    {
        TipoAtoPublicado tipo = TipoAtoPublicado.Criar(
            "AVISO", "Aviso", false, false, false, new DateOnly(2026, 1, 1), null, null).Value!;
        _repository.ObterPorIdAsync(tipo.Id, Arg.Any<CancellationToken>()).Returns(tipo);

        Result resultado = await RemoverTipoAtoPublicadoCommandHandler.Handle(
            new RemoverTipoAtoPublicadoCommand(tipo.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        _repository.Received(1).Remover(tipo);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
