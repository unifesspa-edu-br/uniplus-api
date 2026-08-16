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

    [Fact(DisplayName = "Tipo inválido propaga o erro de domínio sem consultar o campus responsável nem persistir")]
    public async Task Handle_TipoInvalido_RetornaErroSemConsultarCampusNemPersistir()
    {
        Guid campusId = Guid.CreateVersion7();
        CriarLocalOfertaCommand comando = ComandoValido(campusId) with { Tipo = TipoLocalOferta.Nenhum };

        Result<Guid> resultado = await CriarLocalOfertaCommandHandler.Handle(
            comando, _repository, _campusRepository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(LocalOfertaErrorCodes.TipoInvalido);
        await _campusRepository.DidNotReceive().ExisteVivoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Tipo inválido e cidade ausente no mesmo payload acumulam as violações")]
    public async Task Handle_TipoInvalidoECidadeAusente_AcumulaAsViolacoes()
    {
        var comando = new CriarLocalOfertaCommand(TipoLocalOferta.Nenhum, null, null, null, null, null, null);

        Result<Guid> resultado = await CriarLocalOfertaCommandHandler.Handle(
            comando, _repository, _campusRepository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(4);
        resultado.Errors[0].Field.Should().Be("tipo");
        resultado.Errors[1].Field.Should().Be("cidadeCodigoIbge");
    }
}
