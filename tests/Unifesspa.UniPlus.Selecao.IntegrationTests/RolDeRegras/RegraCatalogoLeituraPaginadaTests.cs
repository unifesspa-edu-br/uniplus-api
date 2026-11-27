namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Readers;

/// <summary>
/// A leitura paginada do <c>regras_do_catalogo</c> contra Postgres real. O que se prova aqui não
/// tem como ser provado com mock: a ordem e a comparação do cursor são traduzidas para SQL, e
/// é a concordância entre as duas que decide se um item de fronteira some ou se repete ao
/// virar a página.
/// </summary>
public sealed class RegraCatalogoLeituraPaginadaTests : IClassFixture<RegraCatalogoDbFixture>
{
    private readonly RegraCatalogoDbFixture _fixture;

    public RegraCatalogoLeituraPaginadaTests(RegraCatalogoDbFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// O reader concreto, e não a interface: o assunto destes testes é a tradução para SQL que
    /// só esta implementação faz. Exercitar a porta aqui esconderia justamente o que se quer
    /// provar.
    /// </summary>
    private static RegraCatalogoReader Reader(UniPlusDbContext context) => new(context);

    private static (string Tipo, string Codigo, string Versao) Chave(RegraCatalogo r) =>
        (Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato.CodigoDeContratoExtensions.Codigo(r.Tipo), r.Codigo, r.Versao);

    [Fact(DisplayName = "A ordem é tipo, código e versão — e não a ordem em que o seed inseriu as linhas")]
    public async Task Listar_OrdenaPorTipoCodigoVersao()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();

        (IReadOnlyList<RegraCatalogo> itens, _, _) = await Reader(context)
            .ListarPaginadoAsync(tipo: null, afterSortKey: null, afterId: null, limit: 500, PaginationDirection.Next, CancellationToken.None);

        itens.Should().NotBeEmpty("o seed do catálogo é pré-condição deste teste");
        itens.Select(Chave).Should().BeInAscendingOrder();
    }

    [Fact(DisplayName = "O filtro por tipo devolve somente aquele tipo")]
    public async Task Listar_FiltradaPorTipo_SoTrazAqueleTipo()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();

        (IReadOnlyList<RegraCatalogo> itens, _, _) = await Reader(context)
            .ListarPaginadoAsync(TipoRegra.CriterioDesempate, afterSortKey: null, afterId: null, limit: 500, PaginationDirection.Next, CancellationToken.None);

        itens.Should().NotBeEmpty();
        itens.Should().OnlyContain(r => r.Tipo == TipoRegra.CriterioDesempate);
    }

    [Fact(DisplayName = "Percorrer o catálogo em páginas pequenas visita cada regra exatamente uma vez, na mesma ordem da leitura inteira")]
    public async Task Listar_PaginandoAteOFim_NaoPulaNemRepete()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = Reader(context);

        (IReadOnlyList<RegraCatalogo> tudo, _, _) = await reader
            .ListarPaginadoAsync(tipo: null, afterSortKey: null, afterId: null, limit: 500, PaginationDirection.Next, CancellationToken.None);

        List<RegraCatalogo> percorrido = await PercorrerPaginasAsync(reader, tudo.Count);

        percorrido.Select(Chave).Should().Equal(tudo.Select(Chave),
            "páginas de 3 em 3 têm de reconstruir exatamente a leitura inteira — pular ou repetir na fronteira "
            + "é o modo de falha clássico de cursor cuja comparação discorda do ORDER BY");
    }

    private static async Task<List<RegraCatalogo>> PercorrerPaginasAsync(RegraCatalogoReader reader, int quantidade)
    {
        List<RegraCatalogo> percorrido = [];
        (string SortKey, Guid Id)? ponto = null;
        for (int pagina = 0; pagina <= quantidade + 1; pagina++)
        {
            (IReadOnlyList<RegraCatalogo> itens, _, (string SortKey, Guid Id)? proximo) = await PaginaAsync(reader, ponto, PaginationDirection.Next);
            percorrido.AddRange(itens);
            ponto = proximo;
            if (ponto is null)
            {
                break;
            }
        }

        return percorrido;
    }

    private static Task<(IReadOnlyList<RegraCatalogo>, (string SortKey, Guid Id)?, (string SortKey, Guid Id)?)> PaginaAsync(
        RegraCatalogoReader reader, (string SortKey, Guid Id)? ponto, PaginationDirection direction) =>
        reader.ListarPaginadoAsync(null, ponto?.SortKey, ponto?.Id, 3, direction, CancellationToken.None);

    [Fact(DisplayName = "Navegar para trás a partir da segunda página devolve a primeira, na ordem ascendente")]
    public async Task Listar_ParaTras_VoltaAPaginaAnterior()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = Reader(context);

        (IReadOnlyList<RegraCatalogo> primeira, _, (string SortKey, Guid Id)? proximo) = await PaginaAsync(reader, null, PaginationDirection.Next);
        proximo.Should().NotBeNull("o catálogo tem mais de 3 regras");

        (IReadOnlyList<RegraCatalogo> segunda, (string SortKey, Guid Id)? anterior, _) = await PaginaAsync(reader, proximo, PaginationDirection.Next);
        anterior.Should().NotBeNull("estando na segunda página, existe uma anterior");

        (IReadOnlyList<RegraCatalogo> voltando, _, _) = await PaginaAsync(reader, anterior, PaginationDirection.Prev);

        voltando.Select(Chave).Should().Equal(primeira.Select(Chave));
        segunda.Select(Chave).Should().NotIntersectWith(primeira.Select(Chave));
    }

    [Fact(DisplayName = "Cursor que aponta para regra inexistente devolve página vazia, não a primeira página")]
    public async Task Listar_CursorOrfao_NaoReiniciaDoComeco()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = Reader(context);
        (_, _, (string SortKey, Guid Id)? proximo) = await PaginaAsync(reader, null, PaginationDirection.Next);

        (IReadOnlyList<RegraCatalogo> itens, (string SortKey, Guid Id)? anterior, (string SortKey, Guid Id)? seguinte) = await reader
            .ListarPaginadoAsync(tipo: null, proximo!.Value.SortKey, Guid.CreateVersion7(), limit: 3, PaginationDirection.Next, CancellationToken.None);

        itens.Should().BeEmpty(
            "reiniciar do começo devolveria itens já vistos sob um cursor que o cliente acredita apontar para o meio");
        anterior.Should().BeNull();
        seguinte.Should().BeNull();
    }

    [Fact(DisplayName = "Continuar o cursor de um tipo sob outro tipo, ou sem tipo, é recusado")]
    public async Task Listar_ContinuacaoSobOutroTipo_EhRecusada()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = Reader(context);
        (_, _, (string SortKey, Guid Id)? proximo) = await reader.ListarPaginadoAsync(
            TipoRegra.CriterioDesempate, null, null, 1, PaginationDirection.Next, CancellationToken.None);
        proximo.Should().NotBeNull("o catálogo tem mais de um critério de desempate");

        Func<Task> semTipo = () => reader.ListarPaginadoAsync(
            null, proximo!.Value.SortKey, proximo.Value.Id, 1, PaginationDirection.Next, CancellationToken.None);
        Func<Task> mesmoTipo = () => reader.ListarPaginadoAsync(
            TipoRegra.CriterioDesempate, proximo!.Value.SortKey, proximo.Value.Id, 1, PaginationDirection.Next, CancellationToken.None);

        await semTipo.Should().ThrowAsync<CursorAnchorMismatchException>();
        await mesmoTipo.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "Continuação sem chave é recusada")]
    public async Task Listar_ContinuacaoSemChave_EhRecusada()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();

        Func<Task> act = () => Reader(context).ListarPaginadoAsync(
            null, null, Guid.CreateVersion7(), 1, PaginationDirection.Next, CancellationToken.None);

        await act.Should().ThrowAsync<CursorAnchorMismatchException>();
    }

    [Fact(DisplayName = "A leitura por identidade devolve a versão pedida, com o hash que o domínio congela")]
    public async Task Obter_PorCodigoEVersao_DevolveAVersaoPedida()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = Reader(context);

        (IReadOnlyList<RegraCatalogo> itens, _, _) = await reader
            .ListarPaginadoAsync(tipo: null, afterSortKey: null, afterId: null, limit: 1, PaginationDirection.Next, CancellationToken.None);
        RegraCatalogo esperada = itens[0];

        RegraCatalogo? obtida = await reader.ObterAsync(esperada.Codigo, esperada.Versao, CancellationToken.None);

        obtida.Should().NotBeNull();
        obtida!.Hash.Should().Be(esperada.Hash);
        obtida.Tipo.Should().Be(esperada.Tipo);
    }

    [Fact(DisplayName = "Versão inexistente de um código que existe não devolve outra versão do mesmo código")]
    public async Task Obter_VersaoInexistente_NaoCaiEmOutraVersao()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = Reader(context);

        (IReadOnlyList<RegraCatalogo> itens, _, _) = await reader
            .ListarPaginadoAsync(tipo: null, afterSortKey: null, afterId: null, limit: 1, PaginationDirection.Next, CancellationToken.None);

        RegraCatalogo? obtida = await reader.ObterAsync(itens[0].Codigo, "v999", CancellationToken.None);

        obtida.Should().BeNull("a identidade é o par (codigo, versao) — o código sozinho não elege versão nenhuma");
    }
}
