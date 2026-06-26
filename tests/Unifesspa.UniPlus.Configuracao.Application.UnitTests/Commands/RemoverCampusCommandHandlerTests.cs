namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Campi;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class RemoverCampusCommandHandlerTests
{
    private readonly ICampusRepository _repository = Substitute.For<ICampusRepository>();
    private readonly ILocalOfertaRepository _localOfertaRepository = Substitute.For<ILocalOfertaRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static Campus NovoCampus() =>
        Campus.Criar(
            "CAMar", "Campus Marabá", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, DateTimeOffset.UnixEpoch, null, null).Value!;

    [Fact(DisplayName = "Campus inexistente retorna 404 (NaoEncontrado)")]
    public async Task Handle_NaoEncontrado_Retorna404()
    {
        _repository.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Campus?)null);

        Result resultado = await RemoverCampusCommandHandler.Handle(
            new RemoverCampusCommand(Guid.CreateVersion7()), _repository, _localOfertaRepository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CampusErrorCodes.NaoEncontrado);
    }

    [Fact(DisplayName = "Campus responsável por LocalOferta vivo bloqueia a remoção (409)")]
    public async Task Handle_ComLocalOfertaVivo_Bloqueia()
    {
        Campus campus = NovoCampus();
        _repository.ObterPorIdAsync(campus.Id, Arg.Any<CancellationToken>()).Returns(campus);
        _localOfertaRepository.ExisteVivoComCampusResponsavelAsync(campus.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        Result resultado = await RemoverCampusCommandHandler.Handle(
            new RemoverCampusCommand(campus.Id), _repository, _localOfertaRepository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CampusErrorCodes.RemocaoBloqueadaPorLocalOferta);
        _repository.DidNotReceive().Remover(Arg.Any<Campus>());
    }

    [Fact(DisplayName = "Campus sem dependentes é removido (soft-delete) e commita")]
    public async Task Handle_SemDependentes_Remove()
    {
        Campus campus = NovoCampus();
        _repository.ObterPorIdAsync(campus.Id, Arg.Any<CancellationToken>()).Returns(campus);
        _localOfertaRepository.ExisteVivoComCampusResponsavelAsync(campus.Id, Arg.Any<CancellationToken>())
            .Returns(false);

        Result resultado = await RemoverCampusCommandHandler.Handle(
            new RemoverCampusCommand(campus.Id), _repository, _localOfertaRepository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        _repository.Received(1).Remover(campus);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
