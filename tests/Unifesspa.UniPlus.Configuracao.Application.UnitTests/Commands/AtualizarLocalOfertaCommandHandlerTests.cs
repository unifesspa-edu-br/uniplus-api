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

public sealed class AtualizarLocalOfertaCommandHandlerTests
{
    private readonly ILocalOfertaRepository _repository = Substitute.For<ILocalOfertaRepository>();
    private readonly ICampusRepository _campusRepository = Substitute.For<ICampusRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static LocalOferta Existente() =>
        LocalOferta.Criar(
            TipoLocalOferta.PoloEad, null, "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, TimeProvider.System.GetUtcNow(), null, null).Value!;

    private static AtualizarLocalOfertaCommand Comando(Guid id, Guid? campusId = null) =>
        new(id, TipoLocalOferta.PoloEad, campusId, "1504208", "Marabá", "PA", null, null);

    [Fact(DisplayName = "Local inexistente retorna NaoEncontrado (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrado()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((LocalOferta?)null);

        Result resultado = await AtualizarLocalOfertaCommandHandler.Handle(
            Comando(id), _repository, _campusRepository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(LocalOfertaErrorCodes.NaoEncontrado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Atualização válida persiste")]
    public async Task Handle_Valido_Persiste()
    {
        LocalOferta existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await AtualizarLocalOfertaCommandHandler.Handle(
            Comando(existente.Id), _repository, _campusRepository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Tipo inválido propaga o erro de domínio sem buscar o local por Id nem persistir")]
    public async Task Handle_TipoInvalido_RetornaErroSemBuscarPorIdNemPersistir()
    {
        var comando = new AtualizarLocalOfertaCommand(
            Guid.CreateVersion7(), TipoLocalOferta.Nenhum, null, "1504208", "Marabá", "PA", null, null);

        Result resultado = await AtualizarLocalOfertaCommandHandler.Handle(
            comando, _repository, _campusRepository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(LocalOfertaErrorCodes.TipoInvalido);
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Tipo inválido e cidade ausente no mesmo payload acumulam as violações")]
    public async Task Handle_TipoInvalidoECidadeAusente_AcumulaAsViolacoes()
    {
        var comando = new AtualizarLocalOfertaCommand(
            Guid.CreateVersion7(), TipoLocalOferta.Nenhum, null, null, null, null, null, null);

        Result resultado = await AtualizarLocalOfertaCommandHandler.Handle(
            comando, _repository, _campusRepository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(4);
        resultado.Errors[0].Field.Should().Be("tipo");
        resultado.Errors[1].Field.Should().Be("cidadeCodigoIbge");
    }

    [Fact(DisplayName = "Campus responsável inexistente retorna erro sem persistir")]
    public async Task Handle_CampusResponsavelInexistente_RetornaErroSemPersistir()
    {
        LocalOferta existente = Existente();
        Guid campusId = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        _campusRepository.ExisteVivoAsync(campusId, Arg.Any<CancellationToken>()).Returns(false);

        Result resultado = await AtualizarLocalOfertaCommandHandler.Handle(
            Comando(existente.Id, campusId), _repository, _campusRepository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(LocalOfertaErrorCodes.CampusResponsavelNaoEncontrado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Campus responsável inalterado não consulta a existência de novo")]
    public async Task Handle_CampusResponsavelInalterado_NaoConsultaDeNovo()
    {
        Guid campusId = Guid.CreateVersion7();
        LocalOferta existente = LocalOferta.Criar(
            TipoLocalOferta.PoloEad, campusId, "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, TimeProvider.System.GetUtcNow(), null, null).Value!;
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await AtualizarLocalOfertaCommandHandler.Handle(
            Comando(existente.Id, campusId), _repository, _campusRepository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        await _campusRepository.DidNotReceive().ExisteVivoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
