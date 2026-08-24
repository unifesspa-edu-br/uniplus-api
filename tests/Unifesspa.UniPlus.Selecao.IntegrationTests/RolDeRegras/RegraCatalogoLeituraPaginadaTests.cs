namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Readers;

/// <summary>
/// A leitura paginada do <c>rol_de_regras</c> contra Postgres real. O que se prova aqui não
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
    private static RegraCatalogoReader Reader(SelecaoDbContext context) => new(context);

    private static (string Tipo, string Codigo, string Versao) Chave(RegraCatalogo r) =>
        (r.Tipo.ToCodigo(), r.Codigo, r.Versao);

    [Fact(DisplayName = "A ordem é tipo, código e versão — e não a ordem em que o seed inseriu as linhas")]
    public async Task Listar_OrdenaPorTipoCodigoVersao()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        (IReadOnlyList<RegraCatalogo> itens, Guid? _, Guid? _) = await Reader(context)
            .ListarPaginadoAsync(tipo: null, afterId: null, limit: 500, PaginationDirection.Next, CancellationToken.None);

        itens.Should().NotBeEmpty("o seed do catálogo é pré-condição deste teste");
        itens.Select(Chave).Should().BeInAscendingOrder();
    }

    [Fact(DisplayName = "O filtro por tipo devolve somente aquele tipo")]
    public async Task Listar_FiltradaPorTipo_SoTrazAqueleTipo()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        (IReadOnlyList<RegraCatalogo> itens, Guid? _, Guid? _) = await Reader(context)
            .ListarPaginadoAsync(TipoRegra.CriterioDesempate, afterId: null, limit: 500, PaginationDirection.Next, CancellationToken.None);

        itens.Should().NotBeEmpty();
        itens.Should().OnlyContain(r => r.Tipo == TipoRegra.CriterioDesempate);
    }

    [Fact(DisplayName = "Percorrer o catálogo em páginas pequenas visita cada regra exatamente uma vez, na mesma ordem da leitura inteira")]
    public async Task Listar_PaginandoAteOFim_NaoPulaNemRepete()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = Reader(context);

        (IReadOnlyList<RegraCatalogo> tudo, Guid? _, Guid? _) = await reader
            .ListarPaginadoAsync(tipo: null, afterId: null, limit: 500, PaginationDirection.Next, CancellationToken.None);

        List<RegraCatalogo> percorrido = [];
        Guid? cursor = null;
        // Limite de segurança proporcional ao catálogo: se o cursor não avançasse, o laço
        // pararia aqui em vez de rodar para sempre, e a asserção de igualdade acusaria.
        for (int pagina = 0; pagina <= tudo.Count + 1; pagina++)
        {
            (IReadOnlyList<RegraCatalogo> itens, Guid? _, Guid? proximo) = await reader
                .ListarPaginadoAsync(tipo: null, cursor, limit: 3, PaginationDirection.Next, CancellationToken.None);

            percorrido.AddRange(itens);
            if (proximo is null)
            {
                break;
            }

            cursor = proximo;
        }

        percorrido.Select(Chave).Should().Equal(tudo.Select(Chave),
            "páginas de 3 em 3 têm de reconstruir exatamente a leitura inteira — pular ou repetir na fronteira "
            + "é o modo de falha clássico de cursor cuja comparação discorda do ORDER BY");
    }

    [Fact(DisplayName = "Navegar para trás a partir da segunda página devolve a primeira, na ordem ascendente")]
    public async Task Listar_ParaTras_VoltaAPaginaAnterior()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = Reader(context);

        (IReadOnlyList<RegraCatalogo> primeira, Guid? _, Guid? proximo) = await reader
            .ListarPaginadoAsync(tipo: null, afterId: null, limit: 3, PaginationDirection.Next, CancellationToken.None);
        proximo.Should().NotBeNull("o catálogo tem mais de 3 regras");

        (IReadOnlyList<RegraCatalogo> segunda, Guid? anterior, Guid? _) = await reader
            .ListarPaginadoAsync(tipo: null, proximo, limit: 3, PaginationDirection.Next, CancellationToken.None);
        anterior.Should().NotBeNull("estando na segunda página, existe uma anterior");

        (IReadOnlyList<RegraCatalogo> voltando, Guid? _, Guid? _) = await reader
            .ListarPaginadoAsync(tipo: null, anterior, limit: 3, PaginationDirection.Prev, CancellationToken.None);

        voltando.Select(Chave).Should().Equal(primeira.Select(Chave));
        segunda.Select(Chave).Should().NotIntersectWith(primeira.Select(Chave));
    }

    [Fact(DisplayName = "Cursor que aponta para regra inexistente devolve página vazia, não a primeira página")]
    public async Task Listar_CursorOrfao_NaoReiniciaDoComeco()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        (IReadOnlyList<RegraCatalogo> itens, Guid? anterior, Guid? proximo) = await Reader(context)
            .ListarPaginadoAsync(tipo: null, Guid.CreateVersion7(), limit: 3, PaginationDirection.Next, CancellationToken.None);

        itens.Should().BeEmpty(
            "reiniciar do começo devolveria itens já vistos sob um cursor que o cliente acredita apontar para o meio");
        anterior.Should().BeNull();
        proximo.Should().BeNull();
    }

    [Fact(DisplayName = "A leitura por identidade devolve a versão pedida, com o hash que o domínio congela")]
    public async Task Obter_PorCodigoEVersao_DevolveAVersaoPedida()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = Reader(context);

        (IReadOnlyList<RegraCatalogo> itens, Guid? _, Guid? _) = await reader
            .ListarPaginadoAsync(tipo: null, afterId: null, limit: 1, PaginationDirection.Next, CancellationToken.None);
        RegraCatalogo esperada = itens[0];

        RegraCatalogo? obtida = await reader.ObterAsync(esperada.Codigo, esperada.Versao, CancellationToken.None);

        obtida.Should().NotBeNull();
        obtida!.Hash.Should().Be(esperada.Hash);
        obtida.Tipo.Should().Be(esperada.Tipo);
    }

    [Fact(DisplayName = "Versão inexistente de um código que existe não devolve outra versão do mesmo código")]
    public async Task Obter_VersaoInexistente_NaoCaiEmOutraVersao()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = Reader(context);

        (IReadOnlyList<RegraCatalogo> itens, Guid? _, Guid? _) = await reader
            .ListarPaginadoAsync(tipo: null, afterId: null, limit: 1, PaginationDirection.Next, CancellationToken.None);

        RegraCatalogo? obtida = await reader.ObterAsync(itens[0].Codigo, "v999", CancellationToken.None);

        obtida.Should().BeNull("a identidade é o par (codigo, versao) — o código sozinho não elege versão nenhuma");
    }
}
