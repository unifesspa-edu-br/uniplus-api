namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Queries.OfertasCurso;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class ListarOfertasCursoQueryHandlerTests
{
    private readonly IOfertaCursoRepository _repository = Substitute.For<IOfertaCursoRepository>();

    public ListarOfertasCursoQueryHandlerTests()
    {
        _repository.ListarPaginadoAsync(default!, default!, default, default, default, default, default).ReturnsForAnyArgs(
            ((IReadOnlyList<OfertaCurso>)[], ((string, Guid)?)null, ((string, Guid)?)null));
    }

    [Fact]
    public async Task Handle_TotalNaoSolicitado_NaoConta()
    {
        Result<ListarOfertasCursoResult> resultado = await ListarAsync(false, Guid.NewGuid());
        resultado.Value!.Total.Should().BeNull();
        await _repository.DidNotReceiveWithAnyArgs().ContarAsync(default!, default);
    }

    [Fact]
    public async Task Handle_TotalSolicitado_ContaComMesmoFiltroDaPagina()
    {
        Guid cursoId = Guid.NewGuid();
        _repository.ContarAsync(Arg.Any<FiltroDasOfertasDeCurso>(), Arg.Any<CancellationToken>()).Returns(3);
        Result<ListarOfertasCursoResult> resultado = await ListarAsync(true, cursoId);
        resultado.Value!.Total.Should().Be(3);
        FiltroDasOfertasDeCurso filtro = new(Unifesspa.UniPlus.Kernel.Domain.ValueObjects.TermoDeBusca.Criar("curso").Value, cursoId);
        await _repository.Received(1).ContarAsync(filtro, CancellationToken.None);
        await _repository.Received(1).ListarPaginadoAsync(
            Arg.Any<IReadOnlyList<SortField>>(), filtro, null, null, 20, PaginationDirection.Next, CancellationToken.None);
    }

    private Task<Result<ListarOfertasCursoResult>> ListarAsync(bool incluirTotal, Guid cursoId) =>
        ListarOfertasCursoQueryHandler.Handle(
            new ListarOfertasCursoQuery([], "  CURSO  ", null, null, 20, PaginationDirection.Next, cursoId, incluirTotal),
            _repository, CancellationToken.None);
}
