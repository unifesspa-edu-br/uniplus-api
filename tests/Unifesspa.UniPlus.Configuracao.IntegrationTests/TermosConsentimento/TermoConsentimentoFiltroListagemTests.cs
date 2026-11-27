namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TermosConsentimento;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
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
        Guid comUnderscore = await SeedAsync($"Alfa_{iso}");
        Guid soLetra = await SeedAsync($"AlfaX{iso}");
        Guid comPercent = await SeedAsync($"Beta%{iso}");
        Guid soSequencia = await SeedAsync($"BetaZZ{iso}");
        Guid comBarra = await SeedAsync($"Gama\\{iso}");
        Guid soLetraBarra = await SeedAsync($"GamaX{iso}");
        await ConferirCuringaLiteralAsync($"Alfa_{iso}", comUnderscore, soLetra, "'_' deve ser literal, não curinga de 1 caractere");
        await ConferirCuringaLiteralAsync($"Beta%{iso}", comPercent, soSequencia, "'%' deve ser literal, não curinga de sequência");
        await ConferirCuringaLiteralAsync($"Gama\\{iso}", comBarra, soLetraBarra, "'\\' deve ser literal, não escape de LIKE do usuário");
    }

    private async Task ConferirCuringaLiteralAsync(string busca, Guid comCuringa, Guid semCuringa, string motivo)
    {
        IReadOnlyList<Guid> resultado = await ListarIdsAsync(busca);
        resultado.Should().Contain(comCuringa);
        resultado.Should().NotContain(semCuringa, motivo);
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

        List<Guid> acumulado = await PercorrerPaginasAsync(iso, esperados.Count);
        acumulado.Should().Equal(esperados, "o cursor deve percorrer só o conjunto filtrado, em ordem de Id");
    }

    private async Task<List<Guid>> PercorrerPaginasAsync(string iso, int quantidadeEsperada)
    {
        List<Guid> acumulado = [];
        (string SortKey, Guid Id)? ponto = null;
        do
        {
            (IReadOnlyList<TermoConsentimento> pagina, _, (string SortKey, Guid Id)? proximo) = await PaginarAsync(iso, take: 2, ponto: ponto);
            acumulado.AddRange(pagina.Select(t => t.Id));
            ponto = pagina.Count == 2 ? proximo : null;
        }
        while (ponto is not null && acumulado.Count < quantidadeEsperada);
        return acumulado;
    }

    [Fact(DisplayName = "Navegação bidirecional com filtro: prev volta à página anterior com flags exatas")]
    public async Task Navegacao_Bidirecional_ComFiltro_FlagsExatas()
    {
        string iso = NovoToken();
        Guid[] ids = await CriarTermosParaNavegacaoAsync(iso);
        IReadOnlyList<Guid> ordenados = await ListarIdsAsync(iso);
        ordenados.Should().BeEquivalentTo(ids);
        ordenados.Should().BeInAscendingOrder(
            "a referência sai do mesmo método que as páginas, e sem esta asserção uma listagem que " +
            "devolvesse o conjunto fora de ordem casaria com páginas igualmente fora de ordem");
        await ConferirNavegacaoBidirecionalAsync(iso, ordenados);
    }

    private async Task<Guid[]> CriarTermosParaNavegacaoAsync(string iso) =>
    [
        await SeedAsync($"BiDir {iso} 0"),
        await SeedAsync($"BiDir {iso} 1"),
        await SeedAsync($"BiDir {iso} 2"),
    ];

    private async Task ConferirNavegacaoBidirecionalAsync(string iso, IReadOnlyList<Guid> ordenados)
    {
        (IReadOnlyList<TermoConsentimento> p1, (string SortKey, Guid Id)? p1Ant, (string SortKey, Guid Id)? p1Prox) =
            await PaginarAsync(iso, take: 2, ponto: null, PaginationDirection.Next);
        p1.Select(t => t.Id).Should().Equal(ordenados[0], ordenados[1]);
        p1Ant.Should().BeNull();
        p1Prox!.Value.Id.Should().Be(ordenados[1]);
        (string SortKey, Guid Id)? anterior = await ConferirUltimaPaginaAsync(iso, p1Prox, ordenados);
        (IReadOnlyList<TermoConsentimento> volta, (string SortKey, Guid Id)? voltaAnt, (string SortKey, Guid Id)? voltaProx) =
            await PaginarAsync(iso, take: 2, ponto: anterior, PaginationDirection.Prev);
        volta.Select(t => t.Id).Should().Equal(ordenados[0], ordenados[1]);
        voltaAnt.Should().BeNull();
        voltaProx!.Value.Id.Should().Be(ordenados[1]);
    }

    private async Task<(string SortKey, Guid Id)?> ConferirUltimaPaginaAsync(
        string iso, (string SortKey, Guid Id)? ponto, IReadOnlyList<Guid> ordenados)
    {
        (IReadOnlyList<TermoConsentimento> ultima, (string SortKey, Guid Id)? ultAnt, (string SortKey, Guid Id)? ultProx) =
            await PaginarAsync(iso, take: 2, ponto: ponto, PaginationDirection.Next);
        ultima.Select(t => t.Id).Should().Equal(ordenados[2]);
        ultProx.Should().BeNull();
        ultAnt!.Value.Id.Should().Be(ordenados[2]);
        return ultAnt;
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

        await using UniPlusDbContext ctx = _fixture.CreateDbContext("admin-1105");
        ctx.TermosConsentimento.Add(termo);
        await ctx.SaveChangesAsync();
        return termo.Id;
    }

    private async Task<(IReadOnlyList<TermoConsentimento> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> PaginarAsync(
        string? searchTerm,
        int take,
        (string SortKey, Guid Id)? ponto,
        PaginationDirection direction = PaginationDirection.Next)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        TermoConsentimentoRepository repository = new(ctx);
        return await repository.ListarPaginadoAsync(ponto?.SortKey, ponto?.Id, take, direction, searchTerm, CancellationToken.None);
    }

    private async Task<IReadOnlyList<Guid>> ListarIdsAsync(string? searchTerm, int take = TakeAlto)
    {
        (IReadOnlyList<TermoConsentimento> itens, _, _) = await PaginarAsync(searchTerm, take, ponto: null);
        return [.. itens.Select(t => t.Id)];
    }

    private static string NovoToken() => Guid.NewGuid().ToString("N")[..10];
}
