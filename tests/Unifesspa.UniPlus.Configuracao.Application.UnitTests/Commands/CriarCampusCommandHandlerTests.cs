namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Campi;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CriarCampusCommandHandlerTests
{
    private readonly ICampusRepository _repository = Substitute.For<ICampusRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static CriarCampusCommand ComandoValido() =>
        new("CAMar", "Campus Marabá", "1504208", "Marabá", "PA", null, null, null, null, null);

    [Fact(DisplayName = "Cria o campus, persiste e retorna o Id")]
    public async Task Handle_SiglaLivre_CriaEPersiste()
    {
        _repository.SiglaExisteEntreLivosAsync("CAMar", null, Arg.Any<CancellationToken>())
            .Returns(false);

        Result<Guid> resultado = await CriarCampusCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBe(Guid.Empty);
        await _repository.Received(1).AdicionarAsync(Arg.Any<Campus>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sigla já existente entre vivos retorna conflito (SiglaJaExiste)")]
    public async Task Handle_SiglaDuplicada_RetornaConflito()
    {
        _repository.SiglaExisteEntreLivosAsync("CAMar", null, Arg.Any<CancellationToken>())
            .Returns(true);

        Result<Guid> resultado = await CriarCampusCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CampusErrorCodes.SiglaJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<Campus>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Cidade malformada propaga o erro de domínio sem persistir")]
    public async Task Handle_CidadeMalformada_RetornaErroDeFormato()
    {
        _repository.SiglaExisteEntreLivosAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>())
            .Returns(false);

        CriarCampusCommand comando = ComandoValido() with { CidadeCodigoIbge = "150420" };

        Result<Guid> resultado = await CriarCampusCommandHandler.Handle(
            comando, _repository, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
