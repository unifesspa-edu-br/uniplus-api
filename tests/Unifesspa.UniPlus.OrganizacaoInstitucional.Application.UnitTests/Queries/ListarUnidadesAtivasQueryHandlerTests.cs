namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Consultas;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.Unidades;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ListarUnidadesAtivasQueryHandlerTests
{
    private readonly IUnidadeRepository _repositorio = Substitute.For<IUnidadeRepository>();

    public ListarUnidadesAtivasQueryHandlerTests()
    {
        _repositorio
            .ListarPaginadoAsync(Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<PaginationDirection>(), Arg.Any<FiltroListagemUnidades>(), Arg.Any<CancellationToken>())
            .Returns(([], null, null));
    }

    private Task<Result<ListarUnidadesAtivasResult>> ListarAsync(string? busca, IReadOnlyList<int>? tipos) =>
        ListarUnidadesAtivasQueryHandler.Handle(
            new ListarUnidadesAtivasQuery(null, 20, PaginationDirection.Next, busca, tipos),
            _repositorio,
            CancellationToken.None);

    [Fact(DisplayName = "A busca longa demais é recusada sem consultar o repositório")]
    public async Task BuscaLonga_RecusaSemPaginar()
    {
        Result<ListarUnidadesAtivasResult> resultado = await ListarAsync(new string('a', BuscaPedida.ComprimentoMaximo + 1), null);

        resultado.Error!.Code.Should().Be(ConsultaErrorCodes.BuscaMuitoLonga);
        _repositorio.ReceivedCalls().Should().BeEmpty();
    }

    [Fact(DisplayName = "O tipo inválido é recusado sem consultar o repositório")]
    public async Task TipoInvalido_RecusaSemPaginar()
    {
        Result<ListarUnidadesAtivasResult> resultado = await ListarAsync("ceps", [999]);

        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.FiltroDeTipoInvalido);
        _repositorio.ReceivedCalls().Should().BeEmpty();
    }

    [Fact(DisplayName = "Com a busca e o tipo válidos, projeta a página do repositório em DTO com as âncoras")]
    public async Task FiltroValido_ProjetaAPaginaEmDto()
    {
        Unidade ceps = UnidadeDeTeste.Criar().Value!;
        Guid proxima = Guid.NewGuid();
        _repositorio
            .ListarPaginadoAsync(null, 20, PaginationDirection.Next, Arg.Any<FiltroListagemUnidades>(), Arg.Any<CancellationToken>())
            .Returns(([ceps], null, proxima));

        Result<ListarUnidadesAtivasResult> resultado = await ListarAsync("ceps", [(int)TipoUnidade.Centro]);

        resultado.Value!.Items.Should().ContainSingle().Which.Id.Should().Be(ceps.Id);
        resultado.Value.ProximoAfterId.Should().Be(proxima);
        resultado.Value.AnteriorAfterId.Should().BeNull();
    }

    [Fact(DisplayName = "O repositório recebe o filtro resolvido: o termo como veio e os tipos pedidos")]
    public async Task FiltroValido_ChegaResolvidoAoRepositorio()
    {
        await ListarAsync("ceps", [(int)TipoUnidade.Centro]);

        await _repositorio.Received(1).ListarPaginadoAsync(
            null,
            20,
            PaginationDirection.Next,
            Arg.Is<FiltroListagemUnidades>(filtro => filtro.Termo == "ceps" && filtro.Tipos.SequenceEqual(new[] { TipoUnidade.Centro })),
            Arg.Any<CancellationToken>());
    }
}
