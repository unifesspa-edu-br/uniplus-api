namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TermosConsentimento;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Integração da filtragem server-side por nome na listagem de termos de
/// consentimento (issue #1105) contra Postgres real: busca indexada via
/// pg_trgm (ILIKE acelerado pelo índice GIN, não word_similarity — este não
/// garante casar substrings internas curtas independente do threshold),
/// caixa-insensível, e coerência do cursor keyset sobre o conjunto filtrado —
/// busca e paginação devem descrever o mesmo conjunto de resultados.
/// </summary>
/// <remarks>
/// A fixture compartilha um único banco entre os testes da classe; cada teste
/// usa um token único embutido no <c>Nome</c> e filtra por ele, isolando seus
/// dados das demais linhas que possam coexistir.
/// </remarks>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class TermoConsentimentoFiltroListagemTests
{
    private const int TakeAlto = 100;

    private readonly ConfiguracaoDbFixture _fixture;

    public TermoConsentimentoFiltroListagemTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Busca filtra por nome e é case-insensitive")]
    public async Task Busca_FiltraPorNome_EhCaseInsensitive()
    {
        string iso = NovoToken();
        Guid comToken = await SeedAsync($"Termo Alpha {iso}");
        Guid semToken = await SeedAsync("Termo Sem Marca");

        IReadOnlyList<Guid> minusculo = await ListarIdsAsync(iso.ToLowerInvariant());
        IReadOnlyList<Guid> maiusculo = await ListarIdsAsync(iso.ToUpperInvariant());

        minusculo.Should().Contain(comToken);
        minusculo.Should().NotContain(semToken);
        maiusculo.Should().Contain(comToken, "a busca deve ser caixa-insensível");
    }

    [Fact(DisplayName = "Busca casa um trecho contínuo do termo dentro de um nome mais longo")]
    public async Task Busca_CasaSubstringDentroDeNomeMaisLongo()
    {
        string iso = NovoToken();
        Guid comToken = await SeedAsync($"Termo de Uso da Plataforma {iso} v2");

        IReadOnlyList<Guid> ids = await ListarIdsAsync(iso);

        ids.Should().Contain(comToken, "ILIKE '%termo%' encontra o termo como trecho contínuo do nome mais longo");
    }

    [Fact(DisplayName = "Busca com termo curto (2 caracteres) casa substring interna do nome")]
    public async Task Busca_TermoCurto_CasaSubstringInterna()
    {
        // Regressão: word_similarity (pg_trgm, threshold padrão 0.6) não
        // garantia casar substrings curtas internas ao nome — ex.: "lat" não
        // batia com "Plataforma" mesmo sendo um trecho contínuo real. ILIKE dá
        // semântica de "contains" exata, sem depender de nenhum limiar.
        string iso = NovoToken();
        string marcador = iso[..2];
        Guid comMarcador = await SeedAsync($"Termo {marcador}Interno {iso}");

        IReadOnlyList<Guid> ids = await ListarIdsAsync(marcador + "Interno");

        ids.Should().Contain(comMarcador, "um termo de 2 caracteres presente como substring deve casar via ILIKE");
    }

    [Fact(DisplayName = "Busca sem correspondência textual com o nome não retorna o registro")]
    public async Task Busca_TermoSemRelacao_NaoCasa()
    {
        string iso = NovoToken();
        Guid semRelacao = await SeedAsync($"Termo Alpha {iso}");

        // Token novo, que não aparece como substring de "Termo Alpha <iso>" nem
        // de qualquer outro dado de teste.
        IReadOnlyList<Guid> ids = await ListarIdsAsync(NovoToken());

        ids.Should().NotContain(semRelacao);
    }

    [Fact(DisplayName = "Curingas do LIKE (_, % e \\) no termo são tratados como literais, não wildcards")]
    public async Task Busca_EscapaCuringasLike()
    {
        string iso = NovoToken();
        // Pares com o MESMO comprimento: sem escape, o curinga no termo casaria
        // o caractere/sequência divergente do par, devolvendo o registro errado.
        Guid comUnderscore = await SeedAsync($"Alfa_{iso}");
        Guid soLetra = await SeedAsync($"AlfaX{iso}");
        Guid comPercent = await SeedAsync($"Beta%{iso}");
        Guid soSequencia = await SeedAsync($"BetaZZ{iso}");
        Guid comBarra = await SeedAsync($"Gama\\{iso}");
        Guid soLetraBarra = await SeedAsync($"GamaX{iso}");

        IReadOnlyList<Guid> porUnderscore = await ListarIdsAsync($"Alfa_{iso}");
        porUnderscore.Should().Contain(comUnderscore);
        porUnderscore.Should().NotContain(soLetra, "'_' deve ser literal, não curinga de 1 caractere");

        IReadOnlyList<Guid> porPercent = await ListarIdsAsync($"Beta%{iso}");
        porPercent.Should().Contain(comPercent);
        porPercent.Should().NotContain(soSequencia, "'%' deve ser literal, não curinga de sequência");

        IReadOnlyList<Guid> porBarra = await ListarIdsAsync($"Gama\\{iso}");
        porBarra.Should().Contain(comBarra);
        porBarra.Should().NotContain(soLetraBarra, "'\\' deve ser literal, não escape de LIKE do usuário");
    }

    [Fact(DisplayName = "Cursor avança sobre o conjunto filtrado sem incluir registros de fora do filtro")]
    public async Task Paginacao_AvancaSobreConjuntoFiltrado()
    {
        string iso = NovoToken();
        Guid t1 = await SeedAsync($"Termo Um {iso}");
        await SeedAsync("Termo Fora do Filtro");
        Guid t2 = await SeedAsync($"Termo Dois {iso}");
        await SeedAsync("Outro Fora do Filtro");
        Guid t3 = await SeedAsync($"Termo Tres {iso}");

        // Conjunto filtrado completo, ordenado por Id (referência).
        List<Guid> esperados = [.. (await ListarIdsAsync(iso, TakeAlto)).OrderBy(id => id)];
        esperados.Should().BeEquivalentTo([t1, t2, t3]);

        // Drena em páginas de 2 seguindo o keyset.
        List<Guid> acumulado = [];
        Guid? cursor = null;
        do
        {
            (IReadOnlyList<TermoConsentimento> pagina, _, _) = await PaginarAsync(iso, take: 2, afterId: cursor);
            acumulado.AddRange(pagina.Select(t => t.Id));
            cursor = pagina.Count == 2 ? pagina[^1].Id : null;
        }
        while (cursor is not null && acumulado.Count < esperados.Count);

        acumulado.Should().Equal(esperados, "o cursor deve percorrer só o conjunto filtrado, em ordem de Id");
    }

    [Fact(DisplayName = "Navegação bidirecional com filtro: prev volta à página anterior com flags exatas")]
    public async Task Navegacao_Bidirecional_ComFiltro_FlagsExatas()
    {
        string iso = NovoToken();
        Guid[] ids =
        [
            await SeedAsync($"BiDir {iso} 0"),
            await SeedAsync($"BiDir {iso} 1"),
            await SeedAsync($"BiDir {iso} 2"),
        ];

        (IReadOnlyList<TermoConsentimento> p1, Guid? p1Ant, Guid? p1Prox) =
            await PaginarAsync(iso, take: 2, afterId: null, PaginationDirection.Next);
        p1.Select(t => t.Id).Should().Equal(ids[0], ids[1]);
        p1Ant.Should().BeNull();
        p1Prox.Should().Be(ids[1]);

        (IReadOnlyList<TermoConsentimento> ultima, Guid? ultAnt, Guid? ultProx) =
            await PaginarAsync(iso, take: 2, afterId: p1Prox, PaginationDirection.Next);
        ultima.Select(t => t.Id).Should().Equal(ids[2]);
        ultProx.Should().BeNull();
        ultAnt.Should().Be(ids[2]);

        (IReadOnlyList<TermoConsentimento> volta, Guid? voltaAnt, Guid? voltaProx) =
            await PaginarAsync(iso, take: 2, afterId: ultAnt, PaginationDirection.Prev);
        volta.Select(t => t.Id).Should().Equal(ids[0], ids[1]);
        voltaAnt.Should().BeNull();
        voltaProx.Should().Be(ids[1]);
    }

    [Fact(DisplayName = "Busca nula/vazia não restringe a listagem")]
    public async Task Busca_NulaOuVazia_NaoRestringe()
    {
        Guid id = await SeedAsync($"Termo {NovoToken()}");

        IReadOnlyList<Guid> semFiltro = await ListarIdsAsync(searchTerm: null, TakeAlto);
        IReadOnlyList<Guid> filtroVazio = await ListarIdsAsync(searchTerm: "   ", TakeAlto);

        semFiltro.Should().Contain(id);
        filtroVazio.Should().Contain(id);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private async Task<Guid> SeedAsync(string nome)
    {
        TermoConsentimento termo = TermoConsentimento.Criar(nome, null, null, null).Value!;

        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext("admin-1105");
        ctx.TermosConsentimento.Add(termo);
        await ctx.SaveChangesAsync();
        return termo.Id;
    }

    private async Task<(IReadOnlyList<TermoConsentimento> Itens, Guid? Anterior, Guid? Proximo)> PaginarAsync(
        string? searchTerm,
        int take,
        Guid? afterId,
        PaginationDirection direction = PaginationDirection.Next)
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);
        TermoConsentimentoRepository repository = new(ctx);
        return await repository.ListarPaginadoAsync(afterId, take, direction, searchTerm, CancellationToken.None);
    }

    private async Task<IReadOnlyList<Guid>> ListarIdsAsync(string? searchTerm, int take = TakeAlto)
    {
        (IReadOnlyList<TermoConsentimento> itens, _, _) = await PaginarAsync(searchTerm, take, afterId: null);
        return [.. itens.Select(t => t.Id)];
    }

    private static string NovoToken() => Guid.NewGuid().ToString("N")[..10];
}
