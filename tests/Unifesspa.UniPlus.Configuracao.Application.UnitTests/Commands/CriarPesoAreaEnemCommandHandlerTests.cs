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

public sealed class CriarPesoAreaEnemCommandHandlerTests
{
    private readonly IPesoAreaEnemRepository _repository = Substitute.For<IPesoAreaEnemRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static CriarPesoAreaEnemCommand ComandoValido() =>
        new("Res. 805/2024", GrupoCurso.Tecnologica, 1.50m, 1.00m, 1.00m, 1.00m, 2.00m, "Res. 805/2024 Anexo I", 400m);

    [Fact(DisplayName = "Cria a linha de pesos, persiste e retorna o Id")]
    public async Task Handle_ParLivre_CriaEPersiste()
    {
        _repository.ParExisteEntreVivosAsync("Res. 805/2024", GrupoCurso.Tecnologica, null, Arg.Any<CancellationToken>())
            .Returns(false);

        Result<Guid> resultado = await CriarPesoAreaEnemCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBe(Guid.Empty);
        await _repository.Received(1).AdicionarAsync(Arg.Any<PesoAreaEnem>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Par (resolução, grupo) já existente entre vivos retorna conflito (ParJaExiste)")]
    public async Task Handle_ParDuplicado_RetornaConflito()
    {
        _repository.ParExisteEntreVivosAsync("Res. 805/2024", GrupoCurso.Tecnologica, null, Arg.Any<CancellationToken>())
            .Returns(true);

        Result<Guid> resultado = await CriarPesoAreaEnemCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(PesoAreaEnemErrorCodes.ParJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<PesoAreaEnem>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Peso negativo propaga o erro de domínio sem persistir")]
    public async Task Handle_PesoNegativo_RetornaErroSemPersistir()
    {
        _repository.ParExisteEntreVivosAsync(Arg.Any<string>(), Arg.Any<string>(), null, Arg.Any<CancellationToken>())
            .Returns(false);

        CriarPesoAreaEnemCommand comando = ComandoValido() with { PesoMatematica = -1.00m };

        Result<Guid> resultado = await CriarPesoAreaEnemCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(PesoAreaEnemErrorCodes.PesoNegativo);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
