namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

public sealed class UnidadeFiltroPorTipoEPaginacaoTests : IClassFixture<UnidadeDbFixture>
{
    private readonly ApoioDaListagemDeUnidades _apoio;

    public UnidadeFiltroPorTipoEPaginacaoTests(UnidadeDbFixture fixture)
    {
        _apoio = new ApoioDaListagemDeUnidades(fixture);
    }

    [Fact(DisplayName = "Filtro por um tipo com a busca devolve só as unidades daquele tipo")]
    public async Task Filtra_PorUmTipoComBusca_DevolveSoOTipo()
    {
        string iso = ApoioDaListagemDeUnidades.NovoToken();
        (Guid centro, Guid faculdade, Guid instituto) = await _apoio.SemearUmDeCadaTipoAsync(iso);

        IReadOnlyList<Guid> ids = await _apoio.ListarIdsAsync(new FiltroListagemUnidades(TermoDeBusca.Criar(iso).Value, [TipoUnidade.Centro]));

        ids.Should().Contain(centro);
        ids.Should().NotContain([faculdade, instituto]);
    }

    [Fact(DisplayName = "Filtro por dois tipos com a busca devolve as unidades de qualquer um dos dois")]
    public async Task Filtra_PorDoisTiposComBusca_DevolveOsDoisTipos()
    {
        string iso = ApoioDaListagemDeUnidades.NovoToken();
        (Guid centro, Guid faculdade, Guid instituto) = await _apoio.SemearUmDeCadaTipoAsync(iso);

        IReadOnlyList<Guid> ids = await _apoio.ListarIdsAsync(
            new FiltroListagemUnidades(TermoDeBusca.Criar(iso).Value, [TipoUnidade.Centro, TipoUnidade.Faculdade]));

        ids.Should().Contain([centro, faculdade]);
        ids.Should().NotContain(instituto);
    }

    // Tolerante a poluição: a fixture é compartilhada, então só se afirma o que é deste teste.
    [Fact(DisplayName = "Filtro só por tipo, sem busca, devolve as unidades daquele tipo")]
    public async Task Filtra_SoPorTipo_DevolveSoOTipo()
    {
        (Guid centro, Guid faculdade, Guid instituto) = await _apoio.SemearUmDeCadaTipoAsync(ApoioDaListagemDeUnidades.NovoToken());

        IReadOnlyList<Guid> ids = await _apoio.ListarIdsAsync(new FiltroListagemUnidades(null, [TipoUnidade.Centro]));

        ids.Should().Contain(centro);
        ids.Should().NotContain([faculdade, instituto]);
    }

    [Fact(DisplayName = "Filtro de busca e tipo devolve exatamente as unidades do tipo que contêm o termo")]
    public async Task Paginacao_ConjuntoFiltradoTemSoOsDoTipoComOTermo()
    {
        string iso = ApoioDaListagemDeUnidades.NovoToken();
        Guid[] centros = await _apoio.SemearTresCentrosEDoisOutrosTiposAsync(iso);

        IReadOnlyList<Unidade> filtradas = await _apoio.ListarAsync(new FiltroListagemUnidades(TermoDeBusca.Criar(iso).Value, [TipoUnidade.Centro]));

        filtradas.Select(u => u.Id).Should().BeEquivalentTo(centros);
    }

    [Fact(DisplayName = "Cursor percorre em páginas o conjunto filtrado, em ordem de Id, sem registros de fora do filtro")]
    public async Task Paginacao_CursorPercorreSoOConjuntoFiltrado()
    {
        string iso = ApoioDaListagemDeUnidades.NovoToken();
        await _apoio.SemearTresCentrosEDoisOutrosTiposAsync(iso);
        FiltroListagemUnidades filtro = new(TermoDeBusca.Criar(iso).Value, [TipoUnidade.Centro]);
        List<Guid> esperados = [.. (await _apoio.ListarAsync(filtro)).Select(u => u.Id)];

        List<Guid> acumulado = await _apoio.PercorrerEmPaginasDeDoisAsync(filtro, esperados.Count);

        acumulado.Should().Equal(esperados, "o cursor deve percorrer só o conjunto filtrado, em ordem de Id");
    }

    [Fact(DisplayName = "Filtro vazio (Nenhum) devolve as unidades de todos os tipos")]
    public async Task FiltroNenhum_DevolveTodosOsTipos()
    {
        string iso = ApoioDaListagemDeUnidades.NovoToken();
        (Guid centro, Guid faculdade, Guid instituto) = await _apoio.SemearUmDeCadaTipoAsync(iso);
        Guid reitoria = await _apoio.SemearAsync($"Reitoria {iso}", TipoUnidade.Reitoria);

        IReadOnlyList<Guid> ids = await _apoio.ListarTodasAsync(FiltroListagemUnidades.Nenhum);

        ids.Should().Contain([centro, faculdade, instituto, reitoria]);
    }

    [Fact(DisplayName = "A referência das páginas sai em ordem ascendente de Id")]
    public async Task Navegacao_ReferenciaDasPaginasEstaEmOrdemAscendente()
    {
        (_, IReadOnlyList<Guid> ordenados) = await _apoio.SemearCincoNaOrdemDaPaginacaoAsync();

        ordenados.Should().BeInAscendingOrder(
            "sem esta asserção uma listagem que devolvesse o conjunto fora de ordem casaria com páginas " +
            "igualmente fora de ordem");
    }

    [Fact(DisplayName = "Primeira página para frente não tem anterior e aponta o próximo")]
    public async Task Navegacao_PrimeiraPagina_SemAnteriorComProximo()
    {
        (FiltroListagemUnidades filtro, IReadOnlyList<Guid> ordenados) = await _apoio.SemearCincoNaOrdemDaPaginacaoAsync();

        (IReadOnlyList<Unidade> pagina, Guid? anterior, Guid? proximo) =
            await _apoio.PaginarAsync(filtro, afterId: null, PaginationDirection.Next);

        pagina.Select(u => u.Id).Should().Equal(ordenados[0], ordenados[1]);
        anterior.Should().BeNull();
        proximo.Should().Be(ordenados[1]);
    }

    [Fact(DisplayName = "Segunda página para frente tem anterior e próximo")]
    public async Task Navegacao_SegundaPagina_ComAnteriorEProximo()
    {
        (FiltroListagemUnidades filtro, IReadOnlyList<Guid> ordenados) = await _apoio.SemearCincoNaOrdemDaPaginacaoAsync();

        (IReadOnlyList<Unidade> pagina, Guid? anterior, Guid? proximo) =
            await _apoio.PaginarAsync(filtro, afterId: ordenados[1], PaginationDirection.Next);

        pagina.Select(u => u.Id).Should().Equal(ordenados[2], ordenados[3]);
        anterior.Should().Be(ordenados[2]);
        proximo.Should().Be(ordenados[3]);
    }

    [Fact(DisplayName = "Para trás a partir da segunda página volta à primeira, em ordem ascendente, sem anterior e com próximo")]
    public async Task Navegacao_ParaTras_VoltaAPaginaAnterior()
    {
        (FiltroListagemUnidades filtro, IReadOnlyList<Guid> ordenados) = await _apoio.SemearCincoNaOrdemDaPaginacaoAsync();

        (IReadOnlyList<Unidade> pagina, Guid? anterior, Guid? proximo) =
            await _apoio.PaginarAsync(filtro, afterId: ordenados[2], PaginationDirection.Prev);

        pagina.Select(u => u.Id).Should().Equal(ordenados[0], ordenados[1]);
        anterior.Should().BeNull();
        proximo.Should().Be(ordenados[1]);
    }

    [Fact(DisplayName = "Última página para frente tem anterior e não tem próximo")]
    public async Task Navegacao_UltimaPagina_ComAnteriorSemProximo()
    {
        (FiltroListagemUnidades filtro, IReadOnlyList<Guid> ordenados) = await _apoio.SemearCincoNaOrdemDaPaginacaoAsync();

        (IReadOnlyList<Unidade> pagina, Guid? anterior, Guid? proximo) =
            await _apoio.PaginarAsync(filtro, afterId: ordenados[3], PaginationDirection.Next);

        pagina.Select(u => u.Id).Should().Equal(ordenados[4]);
        anterior.Should().Be(ordenados[4]);
        proximo.Should().BeNull();
    }

}
