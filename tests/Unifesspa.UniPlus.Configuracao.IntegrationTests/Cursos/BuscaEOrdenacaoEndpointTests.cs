namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Cursos;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// A busca e a ordenação escolhidas por quem consulta, exercitadas por HTTP: o
/// que <c>sort</c> ordena, o que <c>q</c> encontra, e o que as duas recusam.
/// </summary>
/// <remarks>
/// Os nomes usados aqui compartilham um prefixo sorteado por teste. A tabela é
/// estática dentro da collection e acumula cursos de outras baterias; sem o
/// prefixo, as linhas alheias se intercalariam e nenhuma sequência esperada seria
/// estável.
/// </remarks>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class BuscaEOrdenacaoEndpointTests
{
    private const string Cursos = "/api/configuracao/cursos";

    private readonly ConfiguracaoEndpointFixture _fixture;

    public BuscaEOrdenacaoEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    // ── Ordenação ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "sort por um campo ordena por ele, e não pela ordem padrão")]
    public async Task Sort_UmCampo_OrdenaPorEle()
    {
        string bloco = Bloco();
        using HttpClient client = _fixture.Factory.CreateClient();

        // Nome e grau em ordens opostas: ordenar por grau tem de produzir sequência
        // diferente da alfabética por nome, que é o padrão.
        await CriarCursoAsync(client, $"{bloco} Alfa", grau: "Tecnólogo");
        await CriarCursoAsync(client, $"{bloco} Beta", grau: "Licenciatura");
        await CriarCursoAsync(client, $"{bloco} Gama", grau: "Bacharelado");

        IReadOnlyList<string> porGrau = await NomesDoBlocoAsync(client, bloco, "sort=grau");

        porGrau.Should().Equal($"{bloco} Gama", $"{bloco} Beta", $"{bloco} Alfa");
    }

    [Fact(DisplayName = "O prefixo '-' inverte o sentido do campo")]
    public async Task Sort_ComPrefixo_InverteOSentido()
    {
        string bloco = Bloco();
        using HttpClient client = _fixture.Factory.CreateClient();

        foreach (string sufixo in new[] { "Alfa", "Beta", "Gama" })
        {
            await CriarCursoAsync(client, $"{bloco} {sufixo}");
        }

        IReadOnlyList<string> decrescente = await NomesDoBlocoAsync(client, bloco, "sort=-nome");

        decrescente.Should().Equal($"{bloco} Gama", $"{bloco} Beta", $"{bloco} Alfa");
    }

    [Fact(DisplayName = "Com vários campos, o primeiro ordena e o seguinte desempata")]
    public async Task Sort_VariosCampos_RespeitaAPrioridade()
    {
        string bloco = Bloco();
        using HttpClient client = _fixture.Factory.CreateClient();

        // Dois graus, três níveis: ordenar por grau e depois por nível decrescente
        // só produz a sequência esperada se a prioridade for respeitada.
        await CriarCursoAsync(client, $"{bloco} Um", grau: "Bacharelado", nivel: "Graduação");
        await CriarCursoAsync(client, $"{bloco} Dois", grau: "Bacharelado", nivel: "Pós-graduação");
        await CriarCursoAsync(client, $"{bloco} Tres", grau: "Licenciatura", nivel: "Graduação");

        IReadOnlyList<string> ordenados = await NomesDoBlocoAsync(client, bloco, "sort=grau,-nivelEnsino");

        ordenados.Should().Equal($"{bloco} Dois", $"{bloco} Um", $"{bloco} Tres");
    }

    [Fact(DisplayName = "Sem sort, vale a ordem alfabética padrão")]
    public async Task SemSort_MantemAOrdemPadrao()
    {
        string bloco = Bloco();
        using HttpClient client = _fixture.Factory.CreateClient();

        await CriarCursoAsync(client, $"{bloco} Gama", grau: "Bacharelado");
        await CriarCursoAsync(client, $"{bloco} Alfa", grau: "Tecnólogo");

        IReadOnlyList<string> padrao = await NomesDoBlocoAsync(client, bloco, consulta: null);

        padrao.Should().Equal($"{bloco} Alfa", $"{bloco} Gama");
    }

    [Theory(DisplayName = "sort com campo que a listagem não ordena devolve 422 nomeando o campo")]
    [InlineData("grupoAreaEnem")]
    [InlineData("createdBy")]
    [InlineData("id")]
    public async Task Sort_CampoNaoOrdenavel_Retorna422(string campo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await client.GetAsync(
            new Uri($"{Cursos}?sort={campo}", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        string corpo = await resposta.Content.ReadAsStringAsync();
        corpo.Should().Contain(campo, "a recusa precisa dizer qual campo foi rejeitado");
        corpo.Should().Contain("nome", "e quais são aceitos");
    }

    [Theory(DisplayName = "sort mal formado devolve 422 sem chegar à consulta")]
    [InlineData("nome,")]
    [InlineData(",nome")]
    [InlineData("-")]
    [InlineData("nome,nome")]
    [InlineData("nome,-nome")]
    public async Task Sort_MalFormado_Retorna422(string expressao)
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await client.GetAsync(
            new Uri($"{Cursos}?sort={Uri.EscapeDataString(expressao)}", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── Busca ─────────────────────────────────────────────────────────────

    [Fact(DisplayName = "q encontra por trecho do nome, ignorando acento e caixa")]
    public async Task Busca_PorTrecho_IgnoraAcentoECaixa()
    {
        string bloco = Bloco();
        using HttpClient client = _fixture.Factory.CreateClient();

        await CriarCursoAsync(client, $"{bloco} Ciências Contábeis");
        await CriarCursoAsync(client, $"{bloco} Engenharia");

        // "CIEN" sem acento e em caixa alta tem de achar "Ciências".
        IReadOnlyList<string> achados = await NomesDoBlocoAsync(client, bloco, "q=CIEN");

        achados.Should().Equal($"{bloco} Ciências Contábeis");
    }

    [Fact(DisplayName = "q com acento encontra o registro escrito sem acento")]
    public async Task Busca_ComAcento_EncontraSemAcento()
    {
        string bloco = Bloco();
        using HttpClient client = _fixture.Factory.CreateClient();

        await CriarCursoAsync(client, $"{bloco} Ciencias Exatas");

        IReadOnlyList<string> achados = await NomesDoBlocoAsync(
            client, bloco, $"q={Uri.EscapeDataString("Ciências")}");

        achados.Should().Equal($"{bloco} Ciencias Exatas");
    }

    [Fact(DisplayName = "q encontra pelo código, não só pelo nome")]
    public async Task Busca_PeloCodigo_Encontra()
    {
        string bloco = Bloco();
        using HttpClient client = _fixture.Factory.CreateClient();

        string codigo = $"{bloco}-ESPECIAL";
        await CriarCursoAsync(client, $"{bloco} Um", codigo: codigo);
        await CriarCursoAsync(client, $"{bloco} Dois");

        IReadOnlyList<JsonElement> achados = await ItensDoBlocoAsync(
            client, bloco, $"q={Uri.EscapeDataString("especial")}");

        achados.Select(i => i.GetProperty("codigo").GetString()).Should().Equal(codigo);
    }

    [Fact(DisplayName = "q encontra código escrito com acento, procurando sem acento")]
    public async Task Busca_CodigoAcentuado_EncontraSemAcento()
    {
        string bloco = Bloco();
        using HttpClient client = _fixture.Factory.CreateClient();

        // O cadastro aceita acento no código, e quem procura raramente o digita.
        string codigo = $"{bloco}-CÓDIGO";
        await CriarCursoAsync(client, $"{bloco} Um", codigo: codigo);

        IReadOnlyList<JsonElement> achados = await ItensDoBlocoAsync(
            client, bloco, $"q={Uri.EscapeDataString("codigo")}");

        achados.Select(i => i.GetProperty("codigo").GetString()).Should().Equal(codigo);
    }

    [Fact(DisplayName = "Caractere nulo no termo não derruba a consulta")]
    public async Task Busca_ComCaractereNulo_NaoQuebra()
    {
        string bloco = Bloco();
        using HttpClient client = _fixture.Factory.CreateClient();

        await CriarCursoAsync(client, $"{bloco} Alfa");

        // Texto no Postgres não admite byte zero: sem descartá-lo, o padrão do LIKE
        // derruba a consulta no provider e a resposta vira 500.
        HttpResponseMessage resposta = await client.GetAsync(
            new Uri($"{Cursos}?q=%00", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory(DisplayName = "Curinga digitado encontra o registro que o contém de verdade")]
    [InlineData("%")]
    [InlineData("_")]
    public async Task Busca_ComCuringa_EncontraQuemOContem(string curinga)
    {
        string bloco = Bloco();
        using HttpClient client = _fixture.Factory.CreateClient();

        // Um nome tem o caractere; o outro não. Distinguir os dois é o que separa
        // "escape funcionando" de "escape desligado": sem escape, o curinga casaria
        // os dois; com o caractere virando texto por engano, não casaria nenhum.
        await CriarCursoAsync(client, $"{bloco} Com {curinga} no meio", codigo: $"{bloco}A");
        await CriarCursoAsync(client, $"{bloco} Sem nada", codigo: $"{bloco}B");

        IReadOnlyList<string> achados = await NomesDoBlocoAsync(
            client, bloco, $"q={Uri.EscapeDataString(curinga)}");

        achados.Should().Equal($"{bloco} Com {curinga} no meio");
    }

    [Theory(DisplayName = "Curingas do LIKE digitados na busca são texto, não coringa")]
    [InlineData("%")]
    [InlineData("_")]
    public async Task Busca_ComCuringa_NaoCasaTudo(string curinga)
    {
        string bloco = Bloco();
        using HttpClient client = _fixture.Factory.CreateClient();

        await CriarCursoAsync(client, $"{bloco} Sem caractere especial", codigo: $"{bloco}A");

        IReadOnlyList<string> achados = await NomesDoBlocoAsync(
            client, bloco, $"q={Uri.EscapeDataString(curinga)}");

        achados.Should().BeEmpty("o curinga tem de ser comparado como texto literal");
    }

    [Fact(DisplayName = "q em branco lista tudo, como se não tivesse sido informado")]
    public async Task Busca_EmBranco_ListaTudo()
    {
        string bloco = Bloco();
        using HttpClient client = _fixture.Factory.CreateClient();

        await CriarCursoAsync(client, $"{bloco} Alfa");
        await CriarCursoAsync(client, $"{bloco} Beta");

        IReadOnlyList<string> comBranco = await NomesDoBlocoAsync(client, bloco, "q=%20");

        comBranco.Should().Equal($"{bloco} Alfa", $"{bloco} Beta");
    }

    // ── Busca e ordenação juntas, sob paginação ───────────────────────────

    [Fact(DisplayName = "A travessia preserva busca e ordenação, sem repetir nem omitir")]
    public async Task Travessia_PreservaBuscaEOrdenacao()
    {
        string bloco = Bloco();
        using HttpClient client = _fixture.Factory.CreateClient();

        foreach (string sufixo in new[] { "Eco", "Bio", "Geo", "Neo", "Duo" })
        {
            await CriarCursoAsync(client, $"{bloco} {sufixo}");
        }

        await CriarCursoAsync(client, $"{bloco} Fora do filtro");

        List<string> percorridos = [];
        string? url = $"{Cursos}?q={bloco}%20&sort=-nome&limit=2";
        int paginas = 0;

        while (url is not null)
        {
            HttpResponseMessage resposta = await client.GetAsync(new Uri(url, UriKind.RelativeOrAbsolute));
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            percorridos.AddRange(await LerNomesAsync(resposta));
            paginas++;
            url = LinkRel(resposta, "next");
        }

        percorridos.Should().Equal(
            $"{bloco} Neo",
            $"{bloco} Geo",
            $"{bloco} Fora do filtro",
            $"{bloco} Eco",
            $"{bloco} Duo",
            $"{bloco} Bio");
        percorridos.Should().OnlyHaveUniqueItems();
        paginas.Should().BeGreaterThan(1, "seis registros não cabem numa página de dois");
    }

    [Fact(DisplayName = "Cursor emitido para uma busca não continua outra")]
    public async Task Cursor_DeOutraBusca_Retorna400()
    {
        string bloco = Bloco();
        using HttpClient client = _fixture.Factory.CreateClient();

        foreach (string sufixo in new[] { "Alfa", "Beta", "Gama" })
        {
            await CriarCursoAsync(client, $"{bloco} {sufixo}");
        }

        HttpResponseMessage primeira = await client.GetAsync(
            new Uri($"{Cursos}?q={bloco}%20&limit=1", UriKind.Relative));
        string proxima = LinkRel(primeira, "next").Should().NotBeNull().And.Subject.ToString()!;

        // O mesmo cursor, com o termo de busca trocado.
        string cursorDeOutraBusca = ParametroCursor(proxima);

        HttpResponseMessage resposta = await client.GetAsync(
            new Uri($"{Cursos}?q=outracoisa&{cursorDeOutraBusca}&direction=next", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "Cursor emitido para uma ordenação não continua outra")]
    public async Task Cursor_DeOutraOrdenacao_Retorna400()
    {
        string bloco = Bloco();
        using HttpClient client = _fixture.Factory.CreateClient();

        foreach (string sufixo in new[] { "Alfa", "Beta", "Gama" })
        {
            await CriarCursoAsync(client, $"{bloco} {sufixo}");
        }

        HttpResponseMessage primeira = await client.GetAsync(
            new Uri($"{Cursos}?q={bloco}%20&sort=nome&limit=1", UriKind.Relative));
        string proxima = LinkRel(primeira, "next").Should().NotBeNull().And.Subject.ToString()!;

        string cursor = ParametroCursor(proxima);

        HttpResponseMessage resposta = await client.GetAsync(
            new Uri($"{Cursos}?q={bloco}%20&sort=-nome&{cursor}&direction=next", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "Ordenar por campo que empata muito ainda pagina sem repetir nem omitir")]
    public async Task Sort_CampoComMuitosEmpates_PaginaSemPerderRegistro()
    {
        string bloco = Bloco();
        using HttpClient client = _fixture.Factory.CreateClient();

        // Cinco cursos com o MESMO grau: a coluna de ordenação empata em todos, e só
        // o Id que o motor acrescenta separa um do outro. É o caso que mostra se a
        // âncora carrega o suficiente para retomar de onde parou.
        foreach (string sufixo in new[] { "Alfa", "Beta", "Gama", "Delta", "Epsilon" })
        {
            await CriarCursoAsync(client, $"{bloco} {sufixo}", grau: "Bacharelado");
        }

        List<string> percorridos = [];
        string? url = $"{Cursos}?q={bloco}%20&sort=grau&limit=2";

        while (url is not null)
        {
            HttpResponseMessage resposta = await client.GetAsync(new Uri(url, UriKind.RelativeOrAbsolute));
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            percorridos.AddRange(await LerNomesAsync(resposta));
            url = LinkRel(resposta, "next");
        }

        percorridos.Should().HaveCount(5).And.OnlyHaveUniqueItems();
    }

    [Fact(DisplayName = "Voltar por prev com ordenação escolhida devolve a página anterior inteira")]
    public async Task Prev_ComOrdenacaoEscolhida_VoltaAPaginaAnterior()
    {
        string bloco = Bloco();
        using HttpClient client = _fixture.Factory.CreateClient();

        foreach (string sufixo in new[] { "Alfa", "Beta", "Gama", "Delta" })
        {
            await CriarCursoAsync(client, $"{bloco} {sufixo}");
        }

        HttpResponseMessage primeira = await client.GetAsync(
            new Uri($"{Cursos}?q={bloco}%20&sort=-nome&limit=2", UriKind.Relative));
        IReadOnlyList<string> nomesPrimeira = await LerNomesAsync(primeira);

        string proxima = LinkRel(primeira, "next").Should().NotBeNull().And.Subject.ToString()!;
        HttpResponseMessage segunda = await client.GetAsync(new Uri(proxima, UriKind.RelativeOrAbsolute));

        string anterior = LinkRel(segunda, "prev").Should().NotBeNull().And.Subject.ToString()!;
        HttpResponseMessage volta = await client.GetAsync(new Uri(anterior, UriKind.RelativeOrAbsolute));

        (await LerNomesAsync(volta)).Should().Equal(nomesPrimeira);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Extrai o par <c>cursor=...</c> de um link de navegação. O <c>?</c> inicial
    /// da query string sai junto: remontar a consulta com ele produziria uma URL
    /// malformada, em que o cursor é ignorado sem erro — e o teste passaria a
    /// afirmar o contrário do que pretende.
    /// </summary>
    private static string ParametroCursor(string url) =>
        new Uri(url, UriKind.RelativeOrAbsolute).Query
            .TrimStart('?')
            .Split('&')
            .Single(p => p.StartsWith("cursor=", StringComparison.Ordinal));

    private static string Bloco() => $"ZZ{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    private static async Task<IReadOnlyList<string>> NomesDoBlocoAsync(
        HttpClient client, string bloco, string? consulta)
    {
        IReadOnlyList<JsonElement> itens = await ItensDoBlocoAsync(client, bloco, consulta);
        return [.. itens.Select(i => i.GetProperty("nome").GetString()!)];
    }

    private static async Task<IReadOnlyList<JsonElement>> ItensDoBlocoAsync(
        HttpClient client, string bloco, string? consulta)
    {
        List<JsonElement> itens = [];
        string? url = consulta is null ? $"{Cursos}?limit=100" : $"{Cursos}?limit=100&{consulta}";

        while (url is not null)
        {
            HttpResponseMessage resposta = await client.GetAsync(new Uri(url, UriKind.RelativeOrAbsolute));
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);

            JsonDocument documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
            itens.AddRange(documento.RootElement.EnumerateArray());
            url = LinkRel(resposta, "next");
        }

        return
        [
            .. itens.Where(i => i.GetProperty("nome").GetString()!
                .StartsWith(bloco, StringComparison.Ordinal)),
        ];
    }

    private static async Task<IReadOnlyList<string>> LerNomesAsync(HttpResponseMessage resposta)
    {
        JsonDocument documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return [.. documento.RootElement.EnumerateArray().Select(i => i.GetProperty("nome").GetString()!)];
    }

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

    private static async Task<Guid> CriarCursoAsync(
        HttpClient client,
        string nome,
        string? codigo = null,
        string grau = "Bacharelado",
        string nivel = "Graduação")
    {
        using HttpRequestMessage request = new(
            HttpMethod.Post, new Uri("/api/configuracao/admin/cursos", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(new
        {
            codigo = codigo ?? $"CUR_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}",
            nome,
            grau,
            nivelEnsino = nivel,
        });

        HttpResponseMessage resposta = await client.SendAsync(request);
        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        return await resposta.Content.ReadFromJsonAsync<Guid>();
    }
}
