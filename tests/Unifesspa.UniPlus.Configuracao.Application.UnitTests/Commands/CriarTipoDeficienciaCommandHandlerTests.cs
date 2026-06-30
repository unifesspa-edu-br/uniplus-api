namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDeficiencia;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CriarTipoDeficienciaCommandHandlerTests
{
    private readonly ITipoDeficienciaRepository _repository = Substitute.For<ITipoDeficienciaRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static CriarTipoDeficienciaCommand ComandoValido() =>
        new("Visual", "Deficiência relacionada à visão");

    [Fact(DisplayName = "Nome livre cria o tipo, persiste e retorna o Id")]
    public async Task Handle_NomeLivre_CriaEPersiste()
    {
        _repository.NomeExisteEntreVivosAsync("Visual", null, Arg.Any<CancellationToken>())
            .Returns(false);

        Result<Guid> resultado = await CriarTipoDeficienciaCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBe(Guid.Empty);
        await _repository.Received(1).AdicionarAsync(Arg.Any<TipoDeficiencia>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Nome já existente entre vivos retorna conflito (NomeJaExiste) sem persistir")]
    public async Task Handle_NomeDuplicado_RetornaConflito()
    {
        _repository.NomeExisteEntreVivosAsync("Visual", null, Arg.Any<CancellationToken>())
            .Returns(true);

        Result<Guid> resultado = await CriarTipoDeficienciaCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.NomeJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<TipoDeficiencia>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Nome inválido propaga o erro de domínio sem persistir")]
    public async Task Handle_NomeInvalido_RetornaErroSemPersistir()
    {
        _repository.NomeExisteEntreVivosAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>())
            .Returns(false);

        CriarTipoDeficienciaCommand comando = ComandoValido() with { Nome = "A" };

        Result<Guid> resultado = await CriarTipoDeficienciaCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.NomeTamanho);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
