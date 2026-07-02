namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Cursos;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class RemoverCursoCommandHandlerTests
{
    private readonly ICursoRepository _repository = Substitute.For<ICursoRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static Curso NovoCurso() =>
        Curso.Criar("ENG_CIVIL", "Engenharia Civil", "Bacharelado", "Graduação", null).Value!;

    [Fact(DisplayName = "Curso inexistente retorna NaoEncontrado (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrado()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((Curso?)null);

        Result resultado = await RemoverCursoCommandHandler.Handle(
            new RemoverCursoCommand(id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CursoErrorCodes.NaoEncontrado);
        _repository.DidNotReceive().Remover(Arg.Any<Curso>());
    }

    [Fact(DisplayName = "Curso sem oferta de curso viva é removido (soft-delete) e commita")]
    public async Task Handle_SemOfertaCurso_Remove()
    {
        Curso curso = NovoCurso();
        _repository.ObterPorIdAsync(curso.Id, Arg.Any<CancellationToken>()).Returns(curso);
        _repository.ReferenciadoPorOfertaCursoVivaAsync(curso.Id, Arg.Any<CancellationToken>())
            .Returns(false);

        Result resultado = await RemoverCursoCommandHandler.Handle(
            new RemoverCursoCommand(curso.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        _repository.Received(1).Remover(curso);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Curso referenciado por oferta de curso viva tem a remoção bloqueada (409)")]
    public async Task Handle_ComOfertaCursoViva_BloqueiaRemocao()
    {
        Curso curso = NovoCurso();
        _repository.ObterPorIdAsync(curso.Id, Arg.Any<CancellationToken>()).Returns(curso);
        _repository.ReferenciadoPorOfertaCursoVivaAsync(curso.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        Result resultado = await RemoverCursoCommandHandler.Handle(
            new RemoverCursoCommand(curso.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CursoErrorCodes.RemocaoBloqueadaPorOfertaCurso);
        _repository.DidNotReceive().Remover(Arg.Any<Curso>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
