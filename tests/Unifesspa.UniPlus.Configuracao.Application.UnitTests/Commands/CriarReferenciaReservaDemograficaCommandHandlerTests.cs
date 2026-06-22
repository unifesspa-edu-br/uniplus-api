namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ReferenciasReservaDemografica;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CriarReferenciaReservaDemograficaCommandHandlerTests
{
    private readonly IReferenciaReservaDemograficaRepository _repository =
        Substitute.For<IReferenciaReservaDemograficaRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static CriarReferenciaReservaDemograficaCommand ComandoValido() =>
        new("2022", 78.50m, 1.20m, 8.40m, "Lei 12.711/2012, art. 10, III");

    [Fact(DisplayName = "Cria a referência, persiste e retorna o Id")]
    public async Task Handle_CensoLivre_CriaEPersiste()
    {
        _repository.CensoExisteEntreLivosAsync("2022", null, Arg.Any<CancellationToken>())
            .Returns(false);

        Result<Guid> resultado = await CriarReferenciaReservaDemograficaCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBe(Guid.Empty);
        await _repository.Received(1).AdicionarAsync(Arg.Any<ReferenciaReservaDemografica>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Censo já existente entre vivos retorna conflito (CensoJaExiste)")]
    public async Task Handle_CensoDuplicado_RetornaConflito()
    {
        _repository.CensoExisteEntreLivosAsync("2022", null, Arg.Any<CancellationToken>())
            .Returns(true);

        Result<Guid> resultado = await CriarReferenciaReservaDemograficaCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ReferenciaReservaDemograficaErrorCodes.CensoJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<ReferenciaReservaDemografica>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Percentual inválido propaga o erro de domínio sem persistir")]
    public async Task Handle_PercentualInvalido_RetornaErroSemPersistir()
    {
        _repository.CensoExisteEntreLivosAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>())
            .Returns(false);

        CriarReferenciaReservaDemograficaCommand comando = ComandoValido() with { PcdPercentual = 120m };

        Result<Guid> resultado = await CriarReferenciaReservaDemograficaCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ReferenciaReservaDemograficaErrorCodes.PercentualForaDeFaixa);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
