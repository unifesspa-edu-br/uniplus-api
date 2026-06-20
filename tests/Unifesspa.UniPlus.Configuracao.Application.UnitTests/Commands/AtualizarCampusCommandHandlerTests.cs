namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Campi;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class AtualizarCampusCommandHandlerTests
{
    private static readonly DateTimeOffset CidadeCarimbadaEm = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly ICampusRepository _repository = Substitute.For<ICampusRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static Campus CampusExistente() =>
        Campus.Criar(
            "CAMar", "Campus Marabá", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, CidadeCarimbadaEm, null, null, null, null, null).Value!;

    [Fact(DisplayName = "S4: PUT sem mudar a cidade preserva cidade_display_atualizado_em")]
    public async Task Handle_CidadeInalterada_PreservaCarimboDeCidade()
    {
        Campus campus = CampusExistente();
        _repository.ObterPorIdAsync(campus.Id, Arg.Any<CancellationToken>()).Returns(campus);

        // Muda só o nome do campus; o trio de cidade permanece igual.
        AtualizarCampusCommand comando = new(
            campus.Id, "CAMar", "Campus Marabá Renomeado", "1504208", "Marabá", "PA",
            null, null, null, null, null);

        Result resultado = await AtualizarCampusCommandHandler.Handle(
            comando, _repository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        campus.Nome.Should().Be("Campus Marabá Renomeado");
        campus.CidadeDisplayAtualizadoEm.Should().Be(CidadeCarimbadaEm,
            "a cidade não mudou, então o carimbo de frescura do display cache é preservado");
    }

    [Fact(DisplayName = "S4: PUT trocando a cidade recarimba cidade_display_atualizado_em")]
    public async Task Handle_CidadeAlterada_RecarimbaCidade()
    {
        Campus campus = CampusExistente();
        _repository.ObterPorIdAsync(campus.Id, Arg.Any<CancellationToken>()).Returns(campus);

        AtualizarCampusCommand comando = new(
            campus.Id, "CAMar", "Campus Marabá", "1501402", "Belém", "PA",
            null, null, null, null, null);

        Result resultado = await AtualizarCampusCommandHandler.Handle(
            comando, _repository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        campus.CidadeNome.Should().Be("Belém");
        campus.CidadeDisplayAtualizadoEm.Should().NotBe(CidadeCarimbadaEm,
            "a cidade mudou, então o carimbo é renovado a partir do TimeProvider");
        campus.CidadeOrigem.Should().Be(ReferenciaCidadeGeo.OrigemGeoApi);
    }
}
