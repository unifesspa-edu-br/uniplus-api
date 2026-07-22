namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.LocaisOferta;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class RemoverLocalOfertaCommandHandlerTests
{
    private readonly ILocalOfertaRepository _repository = Substitute.For<ILocalOfertaRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static LocalOferta NovoLocal() =>
        LocalOferta.Criar(
            TipoLocalOferta.PoloEad, null, "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, DateTimeOffset.UnixEpoch, null, null).Value!;

    [Fact(DisplayName = "Local inexistente retorna 404 (NaoEncontrado)")]
    public async Task Handle_NaoEncontrado_Retorna404()
    {
        _repository.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((LocalOferta?)null);

        Result resultado = await RemoverLocalOfertaCommandHandler.Handle(
            new RemoverLocalOfertaCommand(Guid.CreateVersion7()), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(LocalOfertaErrorCodes.NaoEncontrado);
    }

    [Fact(DisplayName = "Local sem oferta de curso viva é removido (soft-delete) e commita")]
    public async Task Handle_SemOfertaCurso_Remove()
    {
        LocalOferta local = NovoLocal();
        _repository.ObterPorIdAsync(local.Id, Arg.Any<CancellationToken>()).Returns(local);
        _repository.ReferenciadoPorOfertaCursoVivaAsync(local.Id, Arg.Any<CancellationToken>())
            .Returns(false);

        Result resultado = await RemoverLocalOfertaCommandHandler.Handle(
            new RemoverLocalOfertaCommand(local.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        _repository.Received(1).Remover(local);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Local referenciado por oferta de curso viva tem a remoção bloqueada (409)")]
    public async Task Handle_ComOfertaCursoViva_BloqueiaRemocao()
    {
        LocalOferta local = NovoLocal();
        _repository.ObterPorIdAsync(local.Id, Arg.Any<CancellationToken>()).Returns(local);
        _repository.ReferenciadoPorOfertaCursoVivaAsync(local.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        Result resultado = await RemoverLocalOfertaCommandHandler.Handle(
            new RemoverLocalOfertaCommand(local.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(LocalOfertaErrorCodes.RemocaoBloqueadaPorOfertaCurso);
        _repository.DidNotReceive().Remover(Arg.Any<LocalOferta>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
