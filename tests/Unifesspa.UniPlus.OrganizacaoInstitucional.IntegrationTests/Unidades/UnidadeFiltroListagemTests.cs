namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

/// <summary>
/// Integração da filtragem server-side da listagem de unidades
/// contra Postgres real: busca textual (acento/caixa-insensível sobre sigla,
/// nome, código, slug e alias), filtro por tipo (um ou mais valores) e a
/// coerência do cursor keyset sobre o conjunto filtrado.
/// </summary>
/// <remarks>
/// A fixture compartilha um único banco entre os testes da classe; cada teste
/// usa um token único (<c>iso</c>) embutido nos campos pesquisáveis e filtra
/// por ele, isolando seus dados das demais linhas que possam coexistir.
/// </remarks>
public sealed class UnidadeFiltroListagemTests : IClassFixture<UnidadeDbFixture>
{
    private const int TakeAlto = 100;
    private static readonly DateOnly DataInicio = new(2026, 1, 1);

    private readonly UnidadeDbFixture _fixture;

    public UnidadeFiltroListagemTests(UnidadeDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Busca casa contra a sigla")]
    public async Task Busca_CasaContraASigla()
    {
        string iso = NovoToken();
        Guid emSigla = await SemearAsync("Centro Alpha", sigla: $"{iso}SG");

        IReadOnlyList<Guid> ids = await ListarIdsAsync(FiltroDeBusca(iso));

        ids.Should().Contain(emSigla);
    }

    [Fact(DisplayName = "Busca casa contra o nome")]
    public async Task Busca_CasaContraONome()
    {
        string iso = NovoToken();
        Guid emNome = await SemearAsync($"Faculdade {iso}");

        IReadOnlyList<Guid> ids = await ListarIdsAsync(FiltroDeBusca(iso));

        ids.Should().Contain(emNome);
    }

    [Fact(DisplayName = "Busca casa contra o código")]
    public async Task Busca_CasaContraOCodigo()
    {
        string iso = NovoToken();
        Guid emCodigo = await SemearAsync("Instituto Beta", codigo: $"{iso}CD");

        IReadOnlyList<Guid> ids = await ListarIdsAsync(FiltroDeBusca(iso));

        ids.Should().Contain(emCodigo);
    }

    [Fact(DisplayName = "Busca casa contra o slug")]
    public async Task Busca_CasaContraOSlug()
    {
        string iso = NovoToken();
        Guid emSlug = await SemearAsync("Diretoria Gama", slug: $"s{iso}-sl");

        IReadOnlyList<Guid> ids = await ListarIdsAsync(FiltroDeBusca(iso));

        ids.Should().Contain(emSlug);
    }

    [Fact(DisplayName = "Busca casa contra o alias")]
    public async Task Busca_CasaContraOAlias()
    {
        string iso = NovoToken();
        Guid emAlias = await SemearAsync("Núcleo Delta", alias: $"{iso} regional");

        IReadOnlyList<Guid> ids = await ListarIdsAsync(FiltroDeBusca(iso));

        ids.Should().Contain(emAlias);
    }

    [Fact(DisplayName = "Busca não devolve a unidade que não contém o termo")]
    public async Task Busca_NaoDevolveUnidadeSemOTermo()
    {
        string iso = NovoToken();
        Guid comTermo = await SemearAsync($"Faculdade {iso}");
        Guid semTermo = await SemearAsync("Coordenação Sem Marca");

        IReadOnlyList<Guid> ids = await ListarIdsAsync(FiltroDeBusca(iso));

        ids.Should().Contain(comTermo);
        ids.Should().NotContain(semTermo);
    }

    [Fact(DisplayName = "Busca é case-insensitive: termo em minúsculas casa o campo em maiúsculas")]
    public async Task Busca_EhCaseInsensitive()
    {
        string iso = NovoToken();
        Guid id = await SemearAsync($"Faculdade {iso.ToUpperInvariant()}");

        IReadOnlyList<Guid> ids = await ListarIdsAsync(FiltroDeBusca(iso));

        ids.Should().Contain(id);
    }

    [Fact(DisplayName = "Busca é insensível a acento (termo acentuado e sem acento casam o mesmo registro)")]
    public async Task Busca_EhAcentoInsensivel()
    {
        string iso = NovoToken();
        Guid id = await SemearAsync($"Promoção{iso}");

        IReadOnlyList<Unidade> semAcento = await ListarAsync(FiltroDeBusca($"promocao{iso}"));
        IReadOnlyList<Unidade> comAcento = await ListarAsync(FiltroDeBusca($"promoção{iso}"));

        semAcento.Select(u => u.Id).Should().Contain(id);
        comAcento.Select(u => u.Id).Should().Contain(id);
    }

    // Pares com o MESMO comprimento: sem escape, o curinga no termo casaria
    // o caractere/sequência divergente do par, devolvendo o registro errado.
    [Fact(DisplayName = "O curinga _ do LIKE no termo é tratado como literal, não como um caractere qualquer")]
    public async Task Busca_EscapaOUnderscoreDoLike()
    {
        string iso = NovoToken();
        Guid comUnderscore = await SemearAsync($"Alfa_{iso}");
        Guid soLetra = await SemearAsync($"AlfaX{iso}");

        IReadOnlyList<Guid> ids = await ListarIdsAsync(FiltroDeBusca($"Alfa_{iso}"));

        ids.Should().Contain(comUnderscore);
        ids.Should().NotContain(soLetra, "'_' deve ser literal, não curinga de 1 caractere");
    }

    [Fact(DisplayName = "O curinga % do LIKE no termo é tratado como literal, não como uma sequência qualquer")]
    public async Task Busca_EscapaOPercentDoLike()
    {
        string iso = NovoToken();
        Guid comPercent = await SemearAsync($"Beta%{iso}");
        Guid soSequencia = await SemearAsync($"BetaZZ{iso}");

        IReadOnlyList<Guid> ids = await ListarIdsAsync(FiltroDeBusca($"Beta%{iso}"));

        ids.Should().Contain(comPercent);
        ids.Should().NotContain(soSequencia, "'%' deve ser literal, não curinga de sequência");
    }

    [Fact(DisplayName = "Filtro por um tipo com a busca devolve só as unidades daquele tipo")]
    public async Task Filtra_PorUmTipoComBusca_DevolveSoOTipo()
    {
        string iso = NovoToken();
        (Guid centro, Guid faculdade, Guid instituto) = await SemearUmDeCadaTipoAsync(iso);

        IReadOnlyList<Guid> ids = await ListarIdsAsync(new FiltroListagemUnidades(iso, [TipoUnidade.Centro]));

        ids.Should().Contain(centro);
        ids.Should().NotContain([faculdade, instituto]);
    }

    [Fact(DisplayName = "Filtro por dois tipos com a busca devolve as unidades de qualquer um dos dois")]
    public async Task Filtra_PorDoisTiposComBusca_DevolveOsDoisTipos()
    {
        string iso = NovoToken();
        (Guid centro, Guid faculdade, Guid instituto) = await SemearUmDeCadaTipoAsync(iso);

        IReadOnlyList<Guid> ids = await ListarIdsAsync(
            new FiltroListagemUnidades(iso, [TipoUnidade.Centro, TipoUnidade.Faculdade]));

        ids.Should().Contain([centro, faculdade]);
        ids.Should().NotContain(instituto);
    }

    // Tolerante a poluição: a fixture é compartilhada, então só se afirma o que é deste teste.
    [Fact(DisplayName = "Filtro só por tipo, sem busca, devolve as unidades daquele tipo")]
    public async Task Filtra_SoPorTipo_DevolveSoOTipo()
    {
        (Guid centro, Guid faculdade, Guid instituto) = await SemearUmDeCadaTipoAsync(NovoToken());

        IReadOnlyList<Guid> ids = await ListarIdsAsync(new FiltroListagemUnidades(null, [TipoUnidade.Centro]));

        ids.Should().Contain(centro);
        ids.Should().NotContain([faculdade, instituto]);
    }

    [Fact(DisplayName = "Filtro de busca e tipo devolve exatamente as unidades do tipo que contêm o termo")]
    public async Task Paginacao_ConjuntoFiltradoTemSoOsDoTipoComOTermo()
    {
        string iso = NovoToken();
        Guid[] centros = await SemearTresCentrosEDoisOutrosTiposAsync(iso);

        IReadOnlyList<Unidade> filtradas = await ListarAsync(new FiltroListagemUnidades(iso, [TipoUnidade.Centro]));

        filtradas.Select(u => u.Id).Should().BeEquivalentTo(centros);
    }

    [Fact(DisplayName = "Cursor percorre em páginas o conjunto filtrado, em ordem de Id, sem registros de fora do filtro")]
    public async Task Paginacao_CursorPercorreSoOConjuntoFiltrado()
    {
        string iso = NovoToken();
        await SemearTresCentrosEDoisOutrosTiposAsync(iso);
        FiltroListagemUnidades filtro = new(iso, [TipoUnidade.Centro]);
        List<Guid> esperados = [.. (await ListarAsync(filtro)).Select(u => u.Id)];

        List<Guid> acumulado = await PercorrerEmPaginasDeDoisAsync(filtro, esperados.Count);

        acumulado.Should().Equal(esperados, "o cursor deve percorrer só o conjunto filtrado, em ordem de Id");
    }

    [Fact(DisplayName = "Filtro vazio (Nenhum) devolve as unidades de todos os tipos")]
    public async Task FiltroNenhum_DevolveTodosOsTipos()
    {
        string iso = NovoToken();
        (Guid centro, Guid faculdade, Guid instituto) = await SemearUmDeCadaTipoAsync(iso);
        Guid reitoria = await SemearAsync($"Reitoria {iso}", TipoUnidade.Reitoria);

        IReadOnlyList<Guid> ids = await ListarTodasAsync(FiltroListagemUnidades.Nenhum);

        ids.Should().Contain([centro, faculdade, instituto, reitoria]);
    }

    [Fact(DisplayName = "A referência das páginas sai em ordem ascendente de Id")]
    public async Task Navegacao_ReferenciaDasPaginasEstaEmOrdemAscendente()
    {
        (_, IReadOnlyList<Guid> ordenados) = await SemearCincoNaOrdemDaPaginacaoAsync();

        ordenados.Should().BeInAscendingOrder(
            "sem esta asserção uma listagem que devolvesse o conjunto fora de ordem casaria com páginas " +
            "igualmente fora de ordem");
    }

    [Fact(DisplayName = "Primeira página para frente não tem anterior e aponta o próximo")]
    public async Task Navegacao_PrimeiraPagina_SemAnteriorComProximo()
    {
        (FiltroListagemUnidades filtro, IReadOnlyList<Guid> ordenados) = await SemearCincoNaOrdemDaPaginacaoAsync();

        (IReadOnlyList<Unidade> pagina, Guid? anterior, Guid? proximo) =
            await PaginarAsync(filtro, afterId: null, PaginationDirection.Next);

        pagina.Select(u => u.Id).Should().Equal(ordenados[0], ordenados[1]);
        anterior.Should().BeNull();
        proximo.Should().Be(ordenados[1]);
    }

    [Fact(DisplayName = "Segunda página para frente tem anterior e próximo")]
    public async Task Navegacao_SegundaPagina_ComAnteriorEProximo()
    {
        (FiltroListagemUnidades filtro, IReadOnlyList<Guid> ordenados) = await SemearCincoNaOrdemDaPaginacaoAsync();

        (IReadOnlyList<Unidade> pagina, Guid? anterior, Guid? proximo) =
            await PaginarAsync(filtro, afterId: ordenados[1], PaginationDirection.Next);

        pagina.Select(u => u.Id).Should().Equal(ordenados[2], ordenados[3]);
        anterior.Should().Be(ordenados[2]);
        proximo.Should().Be(ordenados[3]);
    }

    [Fact(DisplayName = "Para trás a partir da segunda página volta à primeira, em ordem ascendente, sem anterior e com próximo")]
    public async Task Navegacao_ParaTras_VoltaAPaginaAnterior()
    {
        (FiltroListagemUnidades filtro, IReadOnlyList<Guid> ordenados) = await SemearCincoNaOrdemDaPaginacaoAsync();

        (IReadOnlyList<Unidade> pagina, Guid? anterior, Guid? proximo) =
            await PaginarAsync(filtro, afterId: ordenados[2], PaginationDirection.Prev);

        pagina.Select(u => u.Id).Should().Equal(ordenados[0], ordenados[1]);
        anterior.Should().BeNull();
        proximo.Should().Be(ordenados[1]);
    }

    [Fact(DisplayName = "Última página para frente tem anterior e não tem próximo")]
    public async Task Navegacao_UltimaPagina_ComAnteriorSemProximo()
    {
        (FiltroListagemUnidades filtro, IReadOnlyList<Guid> ordenados) = await SemearCincoNaOrdemDaPaginacaoAsync();

        (IReadOnlyList<Unidade> pagina, Guid? anterior, Guid? proximo) =
            await PaginarAsync(filtro, afterId: ordenados[3], PaginationDirection.Next);

        pagina.Select(u => u.Id).Should().Equal(ordenados[4]);
        anterior.Should().Be(ordenados[4]);
        proximo.Should().BeNull();
    }

    private static FiltroListagemUnidades FiltroDeBusca(string termo) => new(termo, []);

    /// <summary>
    /// Grava uma Unidade com a sigla, o código e o slug que o cenário não informar gerados únicos, e devolve o
    /// Id.
    /// </summary>
    private async Task<Guid> SemearAsync(
        string nome,
        TipoUnidade tipo = TipoUnidade.Centro,
        string? sigla = null,
        string? codigo = null,
        string? slug = null,
        string? alias = null)
    {
        Unidade unidade = Unidade.Criar(
            nome,
            new IdentificadoresDaUnidade(slug ?? NovoSlug(), sigla ?? NovoToken(), codigo ?? NovoToken(), alias),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(DataInicio, null),
            null,
            tipo,
            false).Value!;

        await _fixture.GravarAsync("admin-640", unidade);
        return unidade.Id;
    }

    private async Task<(Guid Centro, Guid Faculdade, Guid Instituto)> SemearUmDeCadaTipoAsync(string iso) =>
        (await SemearAsync($"Centro {iso}"),
         await SemearAsync($"Faculdade {iso}", TipoUnidade.Faculdade),
         await SemearAsync($"Instituto {iso}", TipoUnidade.Instituto));

    /// <summary>Três Centros entre uma Faculdade e um Núcleo, todos com o token no nome; devolve os Centros.</summary>
    private async Task<Guid[]> SemearTresCentrosEDoisOutrosTiposAsync(string iso)
    {
        Guid c1 = await SemearAsync($"Centro Um {iso}");
        await SemearAsync($"Faculdade {iso}", TipoUnidade.Faculdade);
        Guid c2 = await SemearAsync($"Centro Dois {iso}");
        await SemearAsync($"Nucleo {iso}", TipoUnidade.Nucleo);
        Guid c3 = await SemearAsync($"Centro Tres {iso}");
        return [c1, c2, c3];
    }

    /// <summary>
    /// Semeia cinco Unidades e devolve o filtro que as seleciona com o Id delas na ordem em que a paginação as
    /// percorre. A ordem de criação NÃO é a ordem da página: o Id é um Guid v7, e v7 só ordena por tempo quando o
    /// carimbo difere — semeadas no mesmo milissegundo, elas ficam na ordem dos bits aleatórios, diferente a cada
    /// execução. A referência vem do banco, depois de provar que o filtro traz exatamente as cinco.
    /// </summary>
    private async Task<(FiltroListagemUnidades Filtro, IReadOnlyList<Guid> Ordenados)> SemearCincoNaOrdemDaPaginacaoAsync()
    {
        string iso = NovoToken();
        List<Guid> semeadas = [];
        for (int i = 0; i < 5; i++)
        {
            semeadas.Add(await SemearAsync($"BiDir {iso} {i}"));
        }

        FiltroListagemUnidades filtro = FiltroDeBusca(iso);
        IReadOnlyList<Guid> ordenados = await ListarIdsAsync(filtro);
        ordenados.Should().BeEquivalentTo(semeadas);
        return (filtro, ordenados);
    }

    /// <summary>Drena o filtro em páginas de dois, seguindo o cursor, até reunir o total esperado.</summary>
    private async Task<List<Guid>> PercorrerEmPaginasDeDoisAsync(FiltroListagemUnidades filtro, int total)
    {
        List<Guid> acumulado = [];
        Guid? cursor = null;
        do
        {
            (IReadOnlyList<Unidade> pagina, _, _) = await PaginarAsync(filtro, cursor, PaginationDirection.Next);
            acumulado.AddRange(pagina.Select(u => u.Id));
            cursor = pagina.Count == 2 ? pagina[^1].Id : null;
        }
        while (cursor is not null && acumulado.Count < total);

        return acumulado;
    }

    private async Task<(IReadOnlyList<Unidade> Itens, Guid? Anterior, Guid? Proximo)> PaginarAsync(
        FiltroListagemUnidades filtro,
        Guid? afterId,
        PaginationDirection direction)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        var repository = new UnidadeRepository(ctx);
        return await repository.ListarPaginadoAsync(afterId, limit: 2, direction, filtro, CancellationToken.None);
    }

    private async Task<IReadOnlyList<Unidade>> ListarAsync(FiltroListagemUnidades filtro)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        var repository = new UnidadeRepository(ctx);
        (IReadOnlyList<Unidade> itens, _, _) = await repository
            .ListarPaginadoAsync(null, TakeAlto, PaginationDirection.Next, filtro, CancellationToken.None);
        return itens;
    }

    /// <summary>Percorre todas as páginas do filtro, porque a fixture compartilhada acumula unidades de outros testes.</summary>
    private async Task<IReadOnlyList<Guid>> ListarTodasAsync(FiltroListagemUnidades filtro)
    {
        List<Guid> ids = [];
        Guid? cursor = null;
        do
        {
            await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
            var repository = new UnidadeRepository(ctx);
            (IReadOnlyList<Unidade> pagina, _, cursor) = await repository
                .ListarPaginadoAsync(cursor, TakeAlto, PaginationDirection.Next, filtro, CancellationToken.None);
            ids.AddRange(pagina.Select(u => u.Id));
        }
        while (cursor is not null);

        return ids;
    }

    private async Task<IReadOnlyList<Guid>> ListarIdsAsync(FiltroListagemUnidades filtro)
    {
        IReadOnlyList<Unidade> unidades = await ListarAsync(filtro);
        return [.. unidades.Select(u => u.Id)];
    }

    // Tokens hex curtos (sem hífen) servem como sigla/código únicos e como
    // marcador de busca; para slug, compõe-se um formato válido com hífen.
    private static string NovoToken() => Guid.NewGuid().ToString("N")[..10];

    private static string NovoSlug() => $"u-{Guid.NewGuid():N}"[..20];
}
