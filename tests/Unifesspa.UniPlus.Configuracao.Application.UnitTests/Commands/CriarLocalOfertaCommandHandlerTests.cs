namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.LocaisOferta;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CriarLocalOfertaCommandHandlerTests
{
    private readonly ILocalOfertaRepository _repository = Substitute.For<ILocalOfertaRepository>();
    private readonly ICampusRepository _campusRepository = Substitute.For<ICampusRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static CriarLocalOfertaCommand ComandoValido(Guid? campusId = null) =>
        new(TipoLocalOferta.PoloEad, campusId, "1504208", "Marabá", "PA", null, null);

    [Fact(DisplayName = "Cria sem campus responsável e persiste")]
    public async Task Handle_SemCampusResponsavel_Cria()
    {
        Result<Guid> resultado = await CriarLocalOfertaCommandHandler.Handle(
            ComandoValido(), _repository, _campusRepository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        await _repository.Received(1).AdicionarAsync(Arg.Any<LocalOferta>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Campus responsável inexistente retorna 422 (CampusResponsavelNaoEncontrado)")]
    public async Task Handle_CampusResponsavelInexistente_Falha()
    {
        Guid campusId = Guid.CreateVersion7();
        _campusRepository.ExisteVivoAsync(campusId, Arg.Any<CancellationToken>()).Returns(false);

        Result<Guid> resultado = await CriarLocalOfertaCommandHandler.Handle(
            ComandoValido(campusId), _repository, _campusRepository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(LocalOfertaErrorCodes.CampusResponsavelNaoEncontrado);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<LocalOferta>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Campus responsável existente é aceito e persiste")]
    public async Task Handle_CampusResponsavelExistente_Cria()
    {
        Guid campusId = Guid.CreateVersion7();
        _campusRepository.ExisteVivoAsync(campusId, Arg.Any<CancellationToken>()).Returns(true);

        Result<Guid> resultado = await CriarLocalOfertaCommandHandler.Handle(
            ComandoValido(campusId), _repository, _campusRepository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        await _repository.Received(1).AdicionarAsync(Arg.Any<LocalOferta>(), Arg.Any<CancellationToken>());
    }
}
