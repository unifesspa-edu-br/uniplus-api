namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Cursos;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// A ordem alfabética das listagens de Curso e de Oferta de Curso, exercitada
/// por HTTP: a ordem vale para a coleção inteira e não para cada página, sobrevive
/// à travessia nos dois sentidos, e não depende de acentuação nem de caixa.
/// </summary>
/// <remarks>
/// Os nomes usados aqui compartilham um prefixo sorteado por teste, que os agrupa
/// num bloco contíguo da coleção. A tabela é estática dentro da collection e
/// acumula cursos de outros testes; sem o bloco, as linhas alheias se
/// intercalariam e nenhuma sequência esperada seria estável.
/// </remarks>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class OrdenacaoAlfabeticaEndpointTests
{
    private const string Cursos = "/api/configuracao/cursos";
    private const string OfertasCurso = "/api/configuracao/ofertas-curso";

    private readonly ConfiguracaoEndpointFixture _fixture;

    public OrdenacaoAlfabeticaEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Cursos saem em ordem alfabética de nome, e não na ordem em que foram cadastrados")]
    public async Task Cursos_SemOrdenacaoPedida_SaemEmOrdemAlfabetica()
    {
        string bloco = Bloco();
        using HttpClient client = _fixture.Factory.CreateClient();

        // Cadastro em ordem inversa: se a listagem devolvesse a ordem de gravação,
        // a asserção falharia.
        foreach (string sufixo in new[] { "Zoologia", "Matematica", "Biologia", "Agronomia" })
        {
            await CriarCursoAsync(client, $"{bloco} {sufixo}");
        }

        IReadOnlyList<string> nomes = await ListarNomesDoBlocoAsync(client, Cursos, bloco);

        nomes.Should().Equal(
            $"{bloco} Agronomia",
            $"{bloco} Biologia",
            $"{bloco} Matematica",
            $"{bloco} Zoologia");
    }

    [Fact(DisplayName = "Acento e caixa não mudam a posição: a comparação é sobre o nome normalizado")]
    public async Task Cursos_ComAcentoECaixa_OrdenamComoSeNormalizados()
    {
        string bloco = Bloco();
        using HttpClient client = _fixture.Factory.CreateClient();

        foreach (string sufixo in new[] { "Zootecnia", "Ástrofisica", "biologia", "Ciências Contábeis" })
        {
            await CriarCursoAsync(client, $"{bloco} {sufixo}");
        }

        IReadOnlyList<string> nomes = await ListarNomesDoBlocoAsync(client, Cursos, bloco);

        // "Ástrofisica" antes de "biologia" só acontece se o acento for removido
        // antes de comparar; "biologia" minúsculo antes de "Ciências" só se a
        // caixa também sair. Comparando o texto cru, os dois viriam por último.
        nomes.Should().Equal(
            $"{bloco} Ástrofisica",
            $"{bloco} biologia",
            $"{bloco} Ciências Contábeis",
            $"{bloco} Zootecnia");
    }

    [Fact(DisplayName = "Cursos de mesmo nome são desempatados pelo código")]
    public async Task Cursos_MesmoNome_DesempatamPeloCodigo()
    {
        string bloco = Bloco();
        string nome = $"{bloco} Homonimo";
        using HttpClient client = _fixture.Factory.CreateClient();

        // Cadastrados fora da ordem dos códigos; dois cursos vivos não podem
        // compartilhar código, então o desempate observável é entre códigos
        // diferentes sob o mesmo nome.
        await CriarCursoAsync(client, nome, codigo: $"{bloco}-C");
        await CriarCursoAsync(client, nome, codigo: $"{bloco}-A");
        await CriarCursoAsync(client, nome, codigo: $"{bloco}-B");

        IReadOnlyList<JsonElement> itens = await ListarDoBlocoAsync(client, Cursos, bloco);

        itens.Select(i => i.GetProperty("codigo").GetString())
            .Should().Equal($"{bloco}-A", $"{bloco}-B", $"{bloco}-C");
    }

    [Fact(DisplayName = "A travessia por rel=\"next\" mantém a ordem global e não repete nem omite registro")]
    public async Task Cursos_TravessiaCompleta_MantemOrdemSemRepetirNemOmitir()
    {
        string bloco = Bloco();
        using HttpClient client = _fixture.Factory.CreateClient();

        // Sete cursos com limit=2: mais registros do que cabe numa página, e a
        // última página fica incompleta.
        string[] sufixos = ["G", "C", "A", "F", "B", "E", "D"];
        foreach (string sufixo in sufixos)
        {
            await CriarCursoAsync(client, $"{bloco} {sufixo}");
        }

        List<string> percorridos = [];
        List<string> paginas = [];
        string? url = $"{Cursos}?limit=2";

        while (url is not null)
        {
            HttpResponseMessage resposta = await client.GetAsync(new Uri(url, UriKind.RelativeOrAbsolute));
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);

            IReadOnlyList<string> nomes = await LerNomesAsync(resposta);
            percorridos.AddRange(nomes.Where(n => n.StartsWith(bloco, StringComparison.Ordinal)));
            paginas.Add(LinkRel(resposta, "self") ?? url);
            url = LinkRel(resposta, "next");
        }

        string[] esperados = [.. sufixos.Order(StringComparer.Ordinal).Select(s => $"{bloco} {s}")];
        percorridos.Should().Equal(esperados);
        percorridos.Should().OnlyHaveUniqueItems();
        paginas.Count.Should().BeGreaterThan(1, "sete registros não cabem numa página de dois");
    }

    [Fact(DisplayName = "rel=\"prev\" volta exatamente à página anterior, na mesma ordem")]
    public async Task Cursos_Prev_VoltaAPaginaAnterior()
    {
        string bloco = Bloco();
        using HttpClient client = _fixture.Factory.CreateClient();

        foreach (string sufixo in new[] { "A", "B", "C", "D" })
        {
            await CriarCursoAsync(client, $"{bloco} {sufixo}");
        }

        // Navega até achar a página que abre o bloco, depois avança uma e volta.
        HttpResponseMessage primeira = await SeguirAtePaginaComAsync(client, bloco, $"{Cursos}?limit=2");
        IReadOnlyList<string> nomesPrimeira = await LerNomesAsync(primeira);

        string proxima = LinkRel(primeira, "next").Should().NotBeNull().And.Subject.ToString()!;
        HttpResponseMessage segunda = await client.GetAsync(new Uri(proxima, UriKind.RelativeOrAbsolute));

        string anterior = LinkRel(segunda, "prev").Should().NotBeNull().And.Subject.ToString()!;
        HttpResponseMessage volta = await client.GetAsync(new Uri(anterior, UriKind.RelativeOrAbsolute));

        (await LerNomesAsync(volta)).Should().Equal(nomesPrimeira);
    }

    [Fact(DisplayName = "Ofertas de curso saem na ordem alfabética do curso que ofertam")]
    public async Task Ofertas_SaemNaOrdemAlfabeticaDoCurso()
    {
        string bloco = Bloco();
        using HttpClient client = _fixture.Factory.CreateClient();

        // Ofertas criadas fora da ordem alfabética dos cursos que ofertam: se a
        // listagem devolvesse a ordem de gravação, a asserção falharia.
        Guid ofertaZoologia = await SemearOfertaAsync($"{bloco} Zoologia");
        Guid ofertaMedicina = await SemearOfertaAsync($"{bloco} Medicina");
        Guid ofertaAgronomia = await SemearOfertaAsync($"{bloco} Agronomia");

        IReadOnlyList<JsonElement> itens = await ListarTudoAsync(client, OfertasCurso);
        Guid[] doBloco =
        [
            .. itens
                .Select(i => i.GetProperty("id").GetGuid())
                .Where(id => id == ofertaZoologia || id == ofertaMedicina || id == ofertaAgronomia),
        ];

        doBloco.Should().Equal(ofertaAgronomia, ofertaMedicina, ofertaZoologia);
    }

    [Fact(DisplayName = "O filtro por curso continua valendo ao seguir rel=\"next\"")]
    public async Task Ofertas_FiltroPorCurso_SobreviveAPaginacao()
    {
        string bloco = Bloco();
        using HttpClient client = _fixture.Factory.CreateClient();

        Guid curso = await SemearCursoAsync($"{bloco} Filtrado");
        Guid outro = await SemearCursoAsync($"{bloco} Ignorado");
        Guid local = await SemearLocalOfertaAsync();

        List<Guid> doCurso =
        [
            await SemearOfertaDeAsync(curso, local),
            await SemearOfertaDeAsync(curso, local),
            await SemearOfertaDeAsync(curso, local),
        ];
        await SemearOfertaDeAsync(outro, local);

        List<Guid> percorridos = [];
        string? url = $"{OfertasCurso}?cursoId={curso}&limit=2";
        while (url is not null)
        {
            HttpResponseMessage resposta = await client.GetAsync(new Uri(url, UriKind.RelativeOrAbsolute));
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            percorridos.AddRange((await LerItensAsync(resposta)).Select(i => i.GetProperty("id").GetGuid()));
            url = LinkRel(resposta, "next");
        }

        percorridos.Should().BeEquivalentTo(doCurso, "o recorte por curso vale em todas as páginas");
        percorridos.Should().OnlyHaveUniqueItems();
    }

    [Fact(DisplayName = "Cursor cuja chave de ordenação não corresponde à ordenação da rota devolve 400")]
    public async Task Cursor_ComChaveDeOrdenacaoIncompativel_Retorna400()
    {
        // Cursor legítimo — cifrado pela chave do próprio host, com a etiqueta e a
        // direção certas — mas cuja chave de ordenação tem um número de colunas
        // diferente do que esta listagem ordena. É o que um cliente veria ao
        // reapresentar, sob uma ordenação, o cursor emitido por outra.
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        CursorEncoder encoder = escopo.ServiceProvider.GetRequiredService<CursorEncoder>();

        string cursor = await encoder.EncodeAsync(new CursorPayload(
            After: Guid.CreateVersion7().ToString(),
            Limit: 20,
            ResourceTag: "cursos",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(10),
            Direction: PaginationDirection.Next,
            SortKey: SortKeyComposta.Serializar("uma", "chave", "de tres colunas")));

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage resposta = await client.GetAsync(
            new Uri($"{Cursos}?cursor={Uri.EscapeDataString(cursor)}", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        string corpo = await resposta.Content.ReadAsStringAsync();
        corpo.Should().Contain("uniplus.paginacao.cursor-invalido");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string Bloco() => $"ZZZ{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    private static async Task<HttpResponseMessage> SeguirAtePaginaComAsync(HttpClient client, string bloco, string url)
    {
        string? atual = url;
        while (atual is not null)
        {
            HttpResponseMessage resposta = await client.GetAsync(new Uri(atual, UriKind.RelativeOrAbsolute));
            IReadOnlyList<string> nomes = await LerNomesAsync(resposta);
            if (nomes.Any(n => n.StartsWith(bloco, StringComparison.Ordinal)))
            {
                return resposta;
            }

            atual = LinkRel(resposta, "next");
        }

        throw new InvalidOperationException($"Nenhuma página contém o bloco {bloco}.");
    }

    private static async Task<IReadOnlyList<string>> ListarNomesDoBlocoAsync(
        HttpClient client, string colecao, string bloco)
    {
        IReadOnlyList<JsonElement> itens = await ListarDoBlocoAsync(client, colecao, bloco);
        return [.. itens.Select(i => i.GetProperty("nome").GetString()!)];
    }

    private static async Task<IReadOnlyList<JsonElement>> ListarDoBlocoAsync(
        HttpClient client, string colecao, string bloco)
    {
        IReadOnlyList<JsonElement> todos = await ListarTudoAsync(client, colecao);
        return
        [
            .. todos.Where(i =>
                i.TryGetProperty("nome", out JsonElement nome)
                && nome.GetString()!.StartsWith(bloco, StringComparison.Ordinal)),
        ];
    }

    private static async Task<IReadOnlyList<JsonElement>> ListarTudoAsync(HttpClient client, string colecao)
    {
        List<JsonElement> itens = [];
        string? url = $"{colecao}?limit=100";

        while (url is not null)
        {
            HttpResponseMessage resposta = await client.GetAsync(new Uri(url, UriKind.RelativeOrAbsolute));
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            itens.AddRange(await LerItensAsync(resposta));
            url = LinkRel(resposta, "next");
        }

        return itens;
    }

    private static async Task<IReadOnlyList<JsonElement>> LerItensAsync(HttpResponseMessage resposta)
    {
        JsonDocument documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return [.. documento.RootElement.EnumerateArray()];
    }

    private static async Task<IReadOnlyList<string>> LerNomesAsync(HttpResponseMessage resposta) =>
        [.. (await LerItensAsync(resposta)).Select(i => i.GetProperty("nome").GetString()!)];

    private static string? LinkRel(HttpResponseMessage resposta, string rel)
    {
        if (!resposta.Headers.TryGetValues("Link", out IEnumerable<string>? links))
        {
            return null;
        }

        foreach (string parte in links.Single().Split(", ", StringSplitOptions.RemoveEmptyEntries))
        {
            if (!parte.Contains($"rel=\"{rel}\"", StringComparison.Ordinal))
            {
                continue;
            }

            int inicio = parte.IndexOf('<', StringComparison.Ordinal) + 1;
            int fim = parte.IndexOf('>', StringComparison.Ordinal);
            if (inicio > 0 && fim > inicio)
            {
                return parte[inicio..fim];
            }
        }

        return null;
    }

    private static async Task<Guid> CriarCursoAsync(HttpClient client, string nome, string? codigo = null)
    {
        HttpResponseMessage resposta = await PostAdminAsync(
            client,
            "/api/configuracao/admin/cursos",
            new
            {
                codigo = codigo ?? $"CUR_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}",
                nome,
                grau = "Bacharelado",
                nivelEnsino = "Graduação",
            });

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        return await resposta.Content.ReadFromJsonAsync<Guid>();
    }

    private async Task<Guid> SemearOfertaAsync(string nomeDoCurso)
    {
        Guid curso = await SemearCursoAsync(nomeDoCurso);
        Guid local = await SemearLocalOfertaAsync();
        return await SemearOfertaDeAsync(curso, local);
    }

    private async Task<Guid> SemearCursoAsync(string nome)
    {
        Curso curso = Curso.Criar(
            $"CUR_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}",
            nome,
            "Bacharelado",
            "Graduação",
            null).Value!;

        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        ConfiguracaoDbContext dbContext = escopo.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        dbContext.Cursos.Add(curso);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        return curso.Id;
    }

    private async Task<Guid> SemearLocalOfertaAsync()
    {
        LocalOferta local = LocalOferta.Criar(
            TipoLocalOferta.CampusSede,
            null,
            "1504208",
            "Marabá",
            "PA",
            ReferenciaCidadeGeo.OrigemGeoApi,
            DateTimeOffset.UtcNow,
            null,
            null).Value!;

        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        ConfiguracaoDbContext dbContext = escopo.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        dbContext.LocaisOferta.Add(local);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        return local.Id;
    }

    private async Task<Guid> SemearOfertaDeAsync(Guid cursoId, Guid localOfertaId)
    {
        OfertaCurso oferta = OfertaCurso.Criar(
            cursoId,
            localOfertaId,
            UnidadeOfertante.Criar(Guid.CreateVersion7(), "ICH", "Instituto de Ciências Humanas", "CENTRO").Value!,
            "REGULAR",
            "PRESENCIAL",
            "EXTENSIVO",
            "REGULAR",
            ["MATUTINO"],
            null,
            null,
            null,
            null,
            null).Value!;

        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        ConfiguracaoDbContext dbContext = escopo.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        dbContext.OfertasCurso.Add(oferta);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        return oferta.Id;
    }

    private static async Task<HttpResponseMessage> PostAdminAsync(HttpClient client, string rota, object corpo)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(rota, UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(corpo);
        return await client.SendAsync(request);
    }
}
