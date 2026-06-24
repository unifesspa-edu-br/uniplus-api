namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.PesosAreaEnem;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class AtualizarPesoAreaEnemCommandHandlerTests
{
    private const string BaseLegal = "Res. 805/2024 Anexo I";

    private readonly IPesoAreaEnemRepository _repository = Substitute.For<IPesoAreaEnemRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static PesoAreaEnem Existente() =>
        PesoAreaEnem.Criar("Res. 805/2024", GrupoCurso.Tecnologica, 1.50m, 1.00m, 1.00m, 1.00m, 2.00m, 400m, BaseLegal).Value!;

    private static AtualizarPesoAreaEnemCommand Comando(Guid id, decimal mt = 3.00m, decimal corte = 450.000m) =>
        new(id, 2.00m, 1.50m, 1.50m, 1.50m, mt, corte, BaseLegal);

    [Fact(DisplayName = "Linha inexistente retorna NaoEncontrado")]
    public async Task Handle_NaoEncontrado_RetornaErro()
    {
        Guid id = Guid.NewGuid();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((PesoAreaEnem?)null);

        Result resultado = await AtualizarPesoAreaEnemCommandHandler.Handle(
            Comando(id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(PesoAreaEnemErrorCodes.NaoEncontrado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Edição válida persiste e preserva a chave de negócio (CA-04b)")]
    public async Task Handle_DadosValidos_PreservaChave()
    {
        PesoAreaEnem existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await AtualizarPesoAreaEnemCommandHandler.Handle(
            Comando(existente.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.Resolucao.Should().Be("Res. 805/2024");
        existente.GrupoCurso.Valor.Should().Be(GrupoCurso.Tecnologica);
        existente.PesoMatematica.Should().Be(3.00m);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Peso negativo na edição retorna erro e não persiste")]
    public async Task Handle_PesoNegativo_RetornaErroSemPersistir()
    {
        PesoAreaEnem existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await AtualizarPesoAreaEnemCommandHandler.Handle(
            Comando(existente.Id, mt: -1.00m), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(PesoAreaEnemErrorCodes.PesoNegativo);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
