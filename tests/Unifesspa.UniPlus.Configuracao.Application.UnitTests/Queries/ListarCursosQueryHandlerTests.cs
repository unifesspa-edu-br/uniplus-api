namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Queries.Cursos;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class ListarCursosQueryHandlerTests
{
    private readonly ICursoRepository _repository = Substitute.For<ICursoRepository>();

    public ListarCursosQueryHandlerTests()
    {
        _repository.ListarPaginadoAsync(default!, default, default, default, default, default, default).ReturnsForAnyArgs(
            ((IReadOnlyList<Curso>)[], ((string, Guid)?)null, ((string, Guid)?)null));
    }

    [Theory]
    [InlineData("  CURSO  ", "curso")]
    [InlineData("   ", null)]
    public async Task Handle_BuscaComEspacos_EnviaTermoNormalizado(string busca, string? esperado)
    {
        Result<ListarCursosResult> resultado = await ListarAsync(busca);
        resultado.IsSuccess.Should().BeTrue();
        await _repository.Received(1).ListarPaginadoAsync(
            Arg.Any<IReadOnlyList<SortField>>(), Arg.Is<TermoDeBusca?>(termo => termo == null ? esperado == null : termo.Normalizado == esperado),
            null, null, 20, PaginationDirection.Next, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_BuscaComDuzentosCaracteres_Aceita()
    {
        Result<ListarCursosResult> resultado = await ListarAsync("  " + new string('a', 200) + "  ");
        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_BuscaComDuzentosEUmCaracteres_Recusa()
    {
        Result<ListarCursosResult> resultado = await ListarAsync(new string('a', 201));
        resultado.Error!.Code.Should().Be(TermoDeBusca.CodigoDeBuscaMuitoLonga);
        resultado.Error.Message.Should().Be("O texto pesquisado excede 200 caracteres, sem contar os espaços das pontas.");
    }

    [Fact]
    public async Task Handle_CampoDeOrdenacaoInvalido_Recusa()
    {
        Result<ListarCursosResult> resultado = await ListarAsync(null, [new("invalido", SortDirection.Ascending)]);
        resultado.Error!.Code.Should().Be("Consulta.CampoDeOrdenacaoInvalido");
    }

    private Task<Result<ListarCursosResult>> ListarAsync(string? busca, IReadOnlyList<SortField>? ordenacao = null) =>
        ListarCursosQueryHandler.Handle(new ListarCursosQuery(ordenacao ?? [], busca, null, null, 20, PaginationDirection.Next),
            _repository, CancellationToken.None);
}
