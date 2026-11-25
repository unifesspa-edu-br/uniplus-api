namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

internal sealed class ApoioDaListagemDeUnidades
{
    private const int TakeAlto = 100;
    private static readonly DateOnly DataInicio = new(2026, 1, 1);
    private readonly UnidadeDbFixture _fixture;

    internal ApoioDaListagemDeUnidades(UnidadeDbFixture fixture)
    {
        _fixture = fixture;
    }

    internal static FiltroListagemUnidades FiltroDeBusca(string termo) => new(TermoDeBusca.Criar(termo).Value, []);

    /// <summary>
    /// Grava uma Unidade com a sigla, o código e o slug que o cenário não informar gerados únicos, e devolve o
    /// Id.
    /// </summary>
    internal async Task<Guid> SemearAsync(
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

    internal async Task<(Guid Centro, Guid Faculdade, Guid Instituto)> SemearUmDeCadaTipoAsync(string iso) =>
        (await SemearAsync($"Centro {iso}"),
         await SemearAsync($"Faculdade {iso}", TipoUnidade.Faculdade),
         await SemearAsync($"Instituto {iso}", TipoUnidade.Instituto));

    /// <summary>Três Centros entre uma Faculdade e um Núcleo, todos com o token no nome; devolve os Centros.</summary>
    internal async Task<Guid[]> SemearTresCentrosEDoisOutrosTiposAsync(string iso)
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
    internal async Task<(FiltroListagemUnidades Filtro, IReadOnlyList<Guid> Ordenados)> SemearCincoNaOrdemDaPaginacaoAsync()
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
    internal async Task<List<Guid>> PercorrerEmPaginasDeDoisAsync(FiltroListagemUnidades filtro, int total)
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

    internal async Task<(IReadOnlyList<Unidade> Itens, Guid? Anterior, Guid? Proximo)> PaginarAsync(
        FiltroListagemUnidades filtro,
        Guid? afterId,
        PaginationDirection direction)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        var repository = new UnidadeRepository(ctx);
        return await repository.ListarPaginadoAsync(afterId, limit: 2, direction, filtro, CancellationToken.None);
    }

    internal async Task<IReadOnlyList<Unidade>> ListarAsync(FiltroListagemUnidades filtro)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        var repository = new UnidadeRepository(ctx);
        (IReadOnlyList<Unidade> itens, _, _) = await repository
            .ListarPaginadoAsync(null, TakeAlto, PaginationDirection.Next, filtro, CancellationToken.None);
        return itens;
    }

    /// <summary>Percorre todas as páginas do filtro, porque a fixture compartilhada acumula unidades de outros testes.</summary>
    internal async Task<IReadOnlyList<Guid>> ListarTodasAsync(FiltroListagemUnidades filtro)
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

    internal async Task<IReadOnlyList<Guid>> ListarIdsAsync(FiltroListagemUnidades filtro)
    {
        IReadOnlyList<Unidade> unidades = await ListarAsync(filtro);
        return [.. unidades.Select(u => u.Id)];
    }

    // Tokens hex curtos (sem hífen) servem como sigla/código únicos e como
    // marcador de busca; para slug, compõe-se um formato válido com hífen.
    internal static string NovoToken() => Guid.NewGuid().ToString("N")[..10];

    private static string NovoSlug() => $"u-{Guid.NewGuid():N}"[..20];
}
