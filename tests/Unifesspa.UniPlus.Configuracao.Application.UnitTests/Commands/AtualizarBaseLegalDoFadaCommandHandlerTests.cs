namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.BaseLegalDoFada;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class AtualizarBaseLegalDoFadaCommandHandlerTests
{
    private readonly IBaseLegalDoFadaRepository _repository = Substitute.For<IBaseLegalDoFadaRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static BaseLegalDoFada Existente() =>
        BaseLegalDoFada.Criar(
            "PORTARIA",
            "Portaria Unifesspa nº 2514/2023",
            "Institui inclusão regional",
            [("1504208", "Marabá", "PA")]).Value!;

    private static AtualizarBaseLegalDoFadaCommand Comando(Guid id) =>
        new(
            id,
            "PORTARIA",
            "Portaria Unifesspa nº 2514/2023 (revisada)",
            "Institui inclusão regional",
            [new CriarMunicipioDaBaseLegalDoFadaCommand("1501402", "Belém", "PA")]);

    [Fact(DisplayName = "Registro inexistente retorna NaoEncontrado sem persistir")]
    public async Task Handle_Inexistente_RetornaNaoEncontrado()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((BaseLegalDoFada?)null);

        Result resultado = await AtualizarBaseLegalDoFadaCommandHandler.Handle(
            Comando(id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(BaseLegalDoFadaErrorCodes.NaoEncontrado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Payload válido atualiza os campos e persiste")]
    public async Task Handle_PayloadValido_AtualizaEPersiste()
    {
        BaseLegalDoFada existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await AtualizarBaseLegalDoFadaCommandHandler.Handle(
            Comando(existente.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.Municipios.Should().ContainSingle(m => m.CodigoIbge == "1501402");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Payload sem nenhum município é recusado sem persistir e sem alterar o registro existente")]
    public async Task Handle_SemMunicipio_RecusaSemPersistir()
    {
        BaseLegalDoFada existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        AtualizarBaseLegalDoFadaCommand comando = Comando(existente.Id) with { Municipios = [] };

        Result resultado = await AtualizarBaseLegalDoFadaCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().Contain(e => e.Error.Code == BaseLegalDoFadaErrorCodes.SemMunicipios);
        existente.Municipios.Should().ContainSingle(m => m.CodigoIbge == "1504208");
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
