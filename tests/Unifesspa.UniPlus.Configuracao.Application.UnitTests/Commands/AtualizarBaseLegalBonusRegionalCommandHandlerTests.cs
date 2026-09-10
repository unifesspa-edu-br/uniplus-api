namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.BaseLegalBonusRegional;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class AtualizarBaseLegalBonusRegionalCommandHandlerTests
{
    private readonly IBaseLegalBonusRegionalRepository _repository = Substitute.For<IBaseLegalBonusRegionalRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static BaseLegalBonusRegional Existente() =>
        BaseLegalBonusRegional.Criar(
            "PORTARIA",
            "Portaria Unifesspa nº 2514/2023",
            "Institui inclusão regional",
            [("1504208", "Marabá", "PA")]).Value!;

    private static AtualizarBaseLegalBonusRegionalCommand Comando(Guid id) =>
        new(
            id,
            "PORTARIA",
            "Portaria Unifesspa nº 2514/2023 (revisada)",
            "Institui inclusão regional",
            [new CriarBaseLegalBonusRegionalMunicipioCommand("1501402", "Belém", "PA")]);

    [Fact(DisplayName = "Registro inexistente retorna NaoEncontrado sem persistir")]
    public async Task Handle_Inexistente_RetornaNaoEncontrado()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((BaseLegalBonusRegional?)null);

        Result resultado = await AtualizarBaseLegalBonusRegionalCommandHandler.Handle(
            Comando(id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(BaseLegalBonusRegionalErrorCodes.NaoEncontrado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Payload válido atualiza os campos e persiste")]
    public async Task Handle_PayloadValido_AtualizaEPersiste()
    {
        BaseLegalBonusRegional existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await AtualizarBaseLegalBonusRegionalCommandHandler.Handle(
            Comando(existente.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.Municipios.Should().ContainSingle(m => m.CodigoIbge == "1501402");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Payload sem nenhum município é recusado sem persistir e sem alterar o registro existente")]
    public async Task Handle_SemMunicipio_RecusaSemPersistir()
    {
        BaseLegalBonusRegional existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        AtualizarBaseLegalBonusRegionalCommand comando = Comando(existente.Id) with { Municipios = [] };

        Result resultado = await AtualizarBaseLegalBonusRegionalCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().Contain(e => e.Error.Code == BaseLegalBonusRegionalErrorCodes.SemMunicipios);
        existente.Municipios.Should().ContainSingle(m => m.CodigoIbge == "1504208");
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
