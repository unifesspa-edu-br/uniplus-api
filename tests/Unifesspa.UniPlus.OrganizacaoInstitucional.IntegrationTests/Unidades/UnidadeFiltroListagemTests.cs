namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Services;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Repositories;

/// <summary>
/// Integração da filtragem server-side da listagem de unidades (issue #640)
/// contra Postgres real: busca textual (acento/caixa-insensível sobre sigla,
/// nome, código, slug e alias), filtro por tipo (um ou mais valores) e a
/// coerência do cursor keyset sobre o conjunto filtrado.
/// </summary>
/// <remarks>
/// A fixture compartilha um único banco entre os testes da classe; cada teste
/// usa um token único (<c>iso</c>) embutido nos campos pesquisáveis e filtra
/// por ele, isolando seus dados das demais linhas que possam coexistir.
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IClassFixture<T> exige tipo de teste público.")]
public sealed class UnidadeFiltroListagemTests : IClassFixture<UnidadeDbFixture>
{
    private const int TakeAlto = 100;
    private static readonly DateOnly DataInicio = new(2026, 1, 1);

    private readonly UnidadeDbFixture _fixture;

    public UnidadeFiltroListagemTests(UnidadeDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Busca casa contra sigla, nome, código, slug e alias e é case-insensitive")]
    public async Task Busca_CobreTodosOsCampos_EhCaseInsensitive()
    {
        string iso = NovoToken();

        Guid emSigla = await SeedAsync(nome: "Centro Alpha", sigla: $"{iso}SG", codigo: NovoToken(), slug: NovoSlug());
        Guid emNome = await SeedAsync(nome: $"Faculdade {iso}", sigla: NovoToken(), codigo: NovoToken(), slug: NovoSlug());
        Guid emCodigo = await SeedAsync(nome: "Instituto Beta", sigla: NovoToken(), codigo: $"{iso}CD", slug: NovoSlug());
        Guid emSlug = await SeedAsync(nome: "Diretoria Gama", sigla: NovoToken(), codigo: NovoToken(), slug: $"s{iso}-sl");
        Guid emAlias = await SeedAsync(nome: "Núcleo Delta", sigla: NovoToken(), codigo: NovoToken(), slug: NovoSlug(), alias: $"{iso} regional");
        Guid semToken = await SeedAsync(nome: "Coordenação Sem Marca", sigla: NovoToken(), codigo: NovoToken(), slug: NovoSlug());

        // Termo em minúsculas (Guid "N"); o índice persistido é maiúsculo →
        // prova a case-insensibilidade da busca.
        IReadOnlyList<Unidade> resultado = await ListarAsync(FiltroDeBusca(iso));

        IReadOnlyList<Guid> ids = [.. resultado.Select(u => u.Id)];
        ids.Should().Contain([emSigla, emNome, emCodigo, emSlug, emAlias]);
        ids.Should().NotContain(semToken);
    }

    [Fact(DisplayName = "Busca é insensível a acento (termo acentuado e sem acento casam o mesmo registro)")]
    public async Task Busca_EhAcentoInsensivel()
    {
        string iso = NovoToken();
        Guid id = await SeedAsync(nome: $"Promoção{iso}", sigla: NovoToken(), codigo: NovoToken(), slug: NovoSlug());

        IReadOnlyList<Unidade> semAcento = await ListarAsync(FiltroDeBusca($"promocao{iso}"));
        IReadOnlyList<Unidade> comAcento = await ListarAsync(FiltroDeBusca($"promoção{iso}"));

        semAcento.Select(u => u.Id).Should().Contain(id);
        comAcento.Select(u => u.Id).Should().Contain(id);
    }

    [Fact(DisplayName = "Filtro por tipo aceita um ou mais valores e combina com a busca")]
    public async Task Filtra_PorTipo_UmOuMais_ECombinaComBusca()
    {
        string iso = NovoToken();
        Guid centro = await SeedAsync(nome: $"Centro {iso}", sigla: NovoToken(), codigo: NovoToken(), slug: NovoSlug(), tipo: TipoUnidade.Centro);
        Guid faculdade = await SeedAsync(nome: $"Faculdade {iso}", sigla: NovoToken(), codigo: NovoToken(), slug: NovoSlug(), tipo: TipoUnidade.Faculdade);
        Guid instituto = await SeedAsync(nome: $"Instituto {iso}", sigla: NovoToken(), codigo: NovoToken(), slug: NovoSlug(), tipo: TipoUnidade.Instituto);

        // Um valor (q + tipo): só o Centro.
        IReadOnlyList<Guid> umTipo = await ListarIdsAsync(
            new FiltroListagemUnidades(Normalizar(iso), [TipoUnidade.Centro]));
        umTipo.Should().Contain(centro);
        umTipo.Should().NotContain([faculdade, instituto]);

        // Dois valores (q + tipos): Centro e Faculdade, não Instituto.
        IReadOnlyList<Guid> doisTipos = await ListarIdsAsync(
            new FiltroListagemUnidades(Normalizar(iso), [TipoUnidade.Centro, TipoUnidade.Faculdade]));
        doisTipos.Should().Contain([centro, faculdade]);
        doisTipos.Should().NotContain(instituto);

        // Tipo isolado (sem q): tolerante a poluição — meu Centro presente, meus outros ausentes.
        IReadOnlyList<Guid> soTipo = await ListarIdsAsync(
            new FiltroListagemUnidades(null, [TipoUnidade.Centro]));
        soTipo.Should().Contain(centro);
        soTipo.Should().NotContain([faculdade, instituto]);
    }

    [Fact(DisplayName = "Cursor avança sobre o conjunto filtrado sem incluir registros de fora do filtro")]
    public async Task Paginacao_AvancaSobreConjuntoFiltrado()
    {
        string iso = NovoToken();
        // 3 Centro + 2 de outros tipos, todos com o token — o filtro tipo=Centro
        // deve produzir exatamente os 3, paginados em ordem de Id.
        Guid c1 = await SeedAsync(nome: $"Centro Um {iso}", sigla: NovoToken(), codigo: NovoToken(), slug: NovoSlug(), tipo: TipoUnidade.Centro);
        await SeedAsync(nome: $"Faculdade {iso}", sigla: NovoToken(), codigo: NovoToken(), slug: NovoSlug(), tipo: TipoUnidade.Faculdade);
        Guid c2 = await SeedAsync(nome: $"Centro Dois {iso}", sigla: NovoToken(), codigo: NovoToken(), slug: NovoSlug(), tipo: TipoUnidade.Centro);
        await SeedAsync(nome: $"Nucleo {iso}", sigla: NovoToken(), codigo: NovoToken(), slug: NovoSlug(), tipo: TipoUnidade.Nucleo);
        Guid c3 = await SeedAsync(nome: $"Centro Tres {iso}", sigla: NovoToken(), codigo: NovoToken(), slug: NovoSlug(), tipo: TipoUnidade.Centro);

        FiltroListagemUnidades filtro = new(Normalizar(iso), [TipoUnidade.Centro]);

        // Conjunto filtrado completo, ordenado por Id (referência).
        List<Guid> esperados = [.. (await ListarAsync(filtro, TakeAlto)).Select(u => u.Id)];
        esperados.Should().BeEquivalentTo([c1, c2, c3]);

        // Drena em páginas de 2 seguindo o keyset.
        List<Guid> acumulado = [];
        Guid? cursor = null;
        do
        {
            IReadOnlyList<Unidade> pagina = await ListarAsync(filtro, take: 2, afterId: cursor);
            acumulado.AddRange(pagina.Select(u => u.Id));
            cursor = pagina.Count == 2 ? pagina[^1].Id : null;
        }
        while (cursor is not null && acumulado.Count < esperados.Count);

        acumulado.Should().Equal(esperados, "o cursor deve percorrer só o conjunto filtrado, em ordem de Id");
    }

    [Fact(DisplayName = "Filtro vazio (Nenhum) não restringe a listagem")]
    public async Task FiltroNenhum_NaoRestringe()
    {
        Guid id = await SeedAsync(nome: $"Reitoria {NovoToken()}", sigla: NovoToken(), codigo: NovoToken(), slug: NovoSlug(), tipo: TipoUnidade.Reitoria);

        IReadOnlyList<Unidade> resultado = await ListarAsync(FiltroListagemUnidades.Nenhum, TakeAlto);

        resultado.Select(u => u.Id).Should().Contain(id);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static string Normalizar(string termo) => NormalizadorTermoBusca.Normalizar(termo);

    private static FiltroListagemUnidades FiltroDeBusca(string termo) => new(Normalizar(termo), []);

    private async Task<Guid> SeedAsync(
        string nome,
        string sigla,
        string codigo,
        string slug,
        string? alias = null,
        TipoUnidade tipo = TipoUnidade.Centro)
    {
        Unidade unidade = Unidade.Criar(
            nome, alias, Slug.From(slug).Value!, sigla, codigo, null,
            tipo, false, DataInicio, null, OrigemUnidade.CriadoNoUniPlus).Value!;

        await using OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext("admin-640");
        ctx.Unidades.Add(unidade);
        await ctx.SaveChangesAsync();
        return unidade.Id;
    }

    private async Task<IReadOnlyList<Unidade>> ListarAsync(
        FiltroListagemUnidades filtro,
        int take = TakeAlto,
        Guid? afterId = null)
    {
        await using OrganizacaoInstitucionalDbContext ctx = _fixture.CreateDbContext(userId: null);
        var repository = new UnidadeRepository(ctx);
        return await repository.ListarPaginadoAsync(afterId, take, filtro, CancellationToken.None);
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
