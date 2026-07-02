namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Cursos;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CriarCursoCommandHandlerTests
{
    private readonly ICursoRepository _repository = Substitute.For<ICursoRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static CriarCursoCommand ComandoValido() =>
        new("ENG_CIVIL", "Engenharia Civil", "Bacharelado", "Graduação", GrupoCurso.Tecnologica);

    [Fact(DisplayName = "Código livre cria o curso, persiste e retorna o Id")]
    public async Task Handle_CodigoLivre_CriaEPersiste()
    {
        _repository.CodigoExisteEntreVivosAsync("ENG_CIVIL", null, Arg.Any<CancellationToken>())
            .Returns(false);

        Result<Guid> resultado = await CriarCursoCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBe(Guid.Empty);
        await _repository.Received(1).AdicionarAsync(Arg.Any<Curso>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Curso sem grupo de área do ENEM é criado normalmente")]
    public async Task Handle_SemGrupoAreaEnem_CriaEPersiste()
    {
        _repository.CodigoExisteEntreVivosAsync("ENG_CIVIL", null, Arg.Any<CancellationToken>())
            .Returns(false);

        Result<Guid> resultado = await CriarCursoCommandHandler.Handle(
            ComandoValido() with { GrupoAreaEnem = null }, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        await _repository.Received(1).AdicionarAsync(
            Arg.Is<Curso>(c => c.GrupoAreaEnem == null), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código já existente entre vivos retorna conflito (CodigoJaExiste) sem persistir")]
    public async Task Handle_CodigoDuplicado_RetornaConflito()
    {
        _repository.CodigoExisteEntreVivosAsync("ENG_CIVIL", null, Arg.Any<CancellationToken>())
            .Returns(true);

        Result<Guid> resultado = await CriarCursoCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CursoErrorCodes.CodigoJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<Curso>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Grupo de área do ENEM inválido propaga o erro de domínio sem persistir")]
    public async Task Handle_GrupoAreaEnemInvalido_RetornaErroSemPersistir()
    {
        _repository.CodigoExisteEntreVivosAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>())
            .Returns(false);

        CriarCursoCommand comando = ComandoValido() with { GrupoAreaEnem = "Exatas" };

        Result<Guid> resultado = await CriarCursoCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CursoErrorCodes.GrupoAreaEnemInvalido);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
