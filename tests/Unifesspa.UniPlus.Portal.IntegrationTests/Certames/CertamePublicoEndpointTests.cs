namespace Unifesspa.UniPlus.Portal.IntegrationTests.Certames;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using AwesomeAssertions;

using Infrastructure;

using Unifesspa.UniPlus.Portal.Application.Certames;
using Unifesspa.UniPlus.Portal.Application.Certames.Origens;

[Trait("Category", "Integration")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IClassFixture<T> exige tipo de teste público.")]
public sealed class CertamePublicoEndpointTests : IClassFixture<PortalComOrigensSimuladasApiFactory>
{
    private const string Identificador = "medicina-2027";
    private const string Rota = "/api/portal/certames/" + Identificador;
    private const string MediaType = "application/vnd.uniplus.certame-publico.v1+json";

    private static readonly Guid ProcessoId = Guid.Parse("0199a000-0000-7000-8000-000000000001");
    private static readonly Guid CriadorId = Guid.Parse("0199a000-0000-7000-8000-00000000000a");
    private static readonly Guid AvisoId = Guid.Parse("0199a000-0000-7000-8000-00000000000b");
    private static readonly Guid ResultadoId = Guid.Parse("0199a000-0000-7000-8000-00000000000c");
    private static readonly Uri EditalNoAcervo =
        new("https://acervo.example/editais/2026/ceps/edital-abertura/medicina-2027/0199a000-0000-7000-8000-00000000000a/abc.pdf");

    private readonly PortalComOrigensSimuladasApiFactory _factory;

    public CertamePublicoEndpointTests(PortalComOrigensSimuladasApiFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "Certame publicado com atos: anônimo, compõe as duas origens, linha do tempo do mais recente ao mais antigo")]
    public async Task Publicado_ComAtos_ComposicaoCompleta()
    {
        ServirCertame();
        _factory.Registro.Resposta = new LeituraDosAtos(AtosEmOrdemDaOrigem(), Falha: null);

        using HttpClient client = _factory.CreateClient();
        using HttpResponseMessage resposta = await client.GetAsync(new Uri(Rota, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be(MediaType);
        resposta.Headers.CacheControl!.NoCache.Should().BeTrue();
        resposta.Headers.CacheControl.NoStore.Should().BeFalse();
        _factory.Contrato.UltimoIdentificador.Should().Be(Identificador);

        using JsonDocument corpo = await LerAsync(resposta);
        JsonElement raiz = corpo.RootElement;

        raiz.GetProperty("identificador").GetString().Should().Be(Identificador);
        raiz.GetProperty("nome").GetString().Should().Be("Medicina 2027");
        raiz.GetProperty("numeroEdital").GetString().Should().Be("012/2026");
        raiz.GetProperty("situacao").GetString().Should().Be("ultimosDias", "a Portal repassa a situação de Seleção com o vocabulário da vitrine");
        raiz.GetProperty("documentoEditalUrl").GetString().Should().Be(EditalNoAcervo.ToString());
        raiz.GetProperty("dataPublicacao").GetString().Should().Be("2026-12-01", "é a data do edital vigente");
        raiz.GetProperty("aviso").ValueKind.Should().Be(JsonValueKind.Null);

        JsonElement[] linha = [.. raiz.GetProperty("linhaDoTempo").EnumerateArray()];
        linha.Select(e => e.GetProperty("id").GetGuid()).Should().Equal(ResultadoId, AvisoId, CriadorId);

        JsonElement edital = linha[2];
        edital.GetProperty("data").GetString().Should().Be("2026-12-01");
        edital.GetProperty("titulo").GetString().Should().Be("Edital de abertura nº 012/2026");
        edital.GetProperty("numero").GetString().Should().Be("012/2026");
        edital.GetProperty("ano").GetInt32().Should().Be(2026);
        edital.GetProperty("tipo").GetProperty("codigo").GetString().Should().Be("EDITAL_ABERTURA");
        edital.GetProperty("tipo").GetProperty("nome").GetString().Should().Be("Edital de abertura");
        edital.GetProperty("descricao").ValueKind.Should().Be(JsonValueKind.Null);
        edital.GetProperty("documentoUrl").GetString().Should().Be(EditalNoAcervo.ToString());
        linha[0].GetProperty("documentoUrl").ValueKind.Should().Be(JsonValueKind.Null, "só o ato criador tem documento no acervo");

        // Ato sem nome do tipo copiado cai para o código, no título e no tipo.
        JsonElement aviso = linha[1];
        aviso.GetProperty("titulo").GetString().Should().Be("AVISO");
        aviso.GetProperty("tipo").GetProperty("nome").GetString().Should().Be("AVISO");

        // Ato sem número declarado: o número vem ausente e o ano da numeração continua presente.
        aviso.GetProperty("numero").ValueKind.Should().Be(JsonValueKind.Null);
        aviso.GetProperty("ano").GetInt32().Should().Be(2027);
    }

    [Fact(DisplayName = "A resposta não carrega dado pessoal do ato nem campo interno da composição")]
    public async Task Publicado_SemDadoPessoalNemCampoInterno()
    {
        ServirCertame();
        _factory.Registro.Resposta = new LeituraDosAtos(AtosEmOrdemDaOrigem(), Falha: null);

        using HttpClient client = _factory.CreateClient();
        using HttpResponseMessage resposta = await client.GetAsync(new Uri(Rota, UriKind.Relative));

        string texto = await resposta.Content.ReadAsStringAsync();

        texto.Should().NotContainEquivalentOf("assinante");
        texto.Should().NotContainEquivalentOf("processoSeletivoId");
        texto.Should().NotContainEquivalentOf("atoCriadorId");
        texto.Should().NotContainEquivalentOf("degradacao");
        texto.Should().NotContainEquivalentOf("selo");
    }

    [Fact(DisplayName = "Certame não disponível publicamente: 404 uniforme, com revalidação obrigatória")]
    public async Task NaoEncontrado_404Uniforme()
    {
        _factory.Contrato.Resposta = new ConsultaDoCertame.NaoEncontrado();

        using HttpClient client = _factory.CreateClient();
        using HttpResponseMessage resposta = await client.GetAsync(new Uri(Rota, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        resposta.Headers.CacheControl!.NoCache.Should().BeTrue();
        resposta.Headers.RetryAfter.Should().BeNull();

        using JsonDocument corpo = await LerAsync(resposta);
        corpo.RootElement.GetProperty("code").GetString().Should().Be("uniplus.portal.certame.nao_encontrado");
    }

    [Theory(DisplayName = "Identificador fora do formato: o mesmo 404 uniforme, sem consultar a origem")]
    [InlineData("%20")]
    [InlineData("Medicina-2027")]
    [InlineData("medicina--2027")]
    [InlineData("-medicina")]
    [InlineData("ab")]
    public async Task IdentificadorForaDoFormato_404Uniforme(string identificador)
    {
        ServirCertame();
        ContratoDoCertameSimulado contrato = _factory.Contrato;
        string? antes = contrato.UltimoIdentificador;

        using HttpClient client = _factory.CreateClient();
        using HttpResponseMessage resposta = await client.GetAsync(
            new Uri("/api/portal/certames/" + identificador, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using JsonDocument corpo = await LerAsync(resposta);
        corpo.RootElement.GetProperty("code").GetString().Should().Be("uniplus.portal.certame.nao_encontrado");
        contrato.UltimoIdentificador.Should().Be(antes, "valor fora do formato não chega à origem");
    }

    [Fact(DisplayName = "Seleção indisponível: 503 com Retry-After, sem conteúdo e sem guarda em cache")]
    public async Task SelecaoIndisponivel_503ComRetryAfter()
    {
        _factory.Contrato.Resposta = new ConsultaDoCertame.Indisponivel();

        using HttpClient client = _factory.CreateClient();
        using HttpResponseMessage resposta = await client.GetAsync(new Uri(Rota, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        resposta.Headers.RetryAfter!.Delta.Should().Be(
            TimeSpan.FromSeconds(PortalComOrigensSimuladasApiFactory.ReencaminharAposEmSegundos));
        resposta.Headers.CacheControl!.NoStore.Should().BeTrue();

        using JsonDocument corpo = await LerAsync(resposta);
        corpo.RootElement.GetProperty("code").GetString().Should().Be("uniplus.portal.origem_indisponivel");
        corpo.RootElement.TryGetProperty("linhaDoTempo", out _).Should().BeFalse();
    }

    public static TheoryData<string, LeituraDosAtos> LinhasDoTempoDegradadas() => new()
    {
        { "Publicações indisponível", new LeituraDosAtos([], FalhaNaLeituraDosAtos.OrigemIndisponivel) },
        { "falha no meio da paginação", new LeituraDosAtos([AtoCriador()], FalhaNaLeituraDosAtos.FalhaNaPaginacao) },
        { "teto de páginas atingido", new LeituraDosAtos([AtoCriador()], FalhaNaLeituraDosAtos.TetoDePaginas) },
        { "ato criador ausente da linha do tempo", new LeituraDosAtos([AtoAviso()], Falha: null) },
        { "detalhe atrasado em relação a Publicações", new LeituraDosAtos([AtoCriador(), AtoRetificacaoMaisRecente()], Falha: null) },
    };

    [Theory(DisplayName = "Linha do tempo degradada: dados do certame com aviso neutro e sem guarda em cache")]
    [MemberData(nameof(LinhasDoTempoDegradadas))]
    public async Task LinhaDoTempoDegradada_DadosComAvisoSemCache(string caso, LeituraDosAtos leitura)
    {
        ArgumentNullException.ThrowIfNull(caso);
        ServirCertame();
        _factory.Registro.Resposta = leitura;

        using HttpClient client = _factory.CreateClient();
        using HttpResponseMessage resposta = await client.GetAsync(new Uri(Rota, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, caso);
        resposta.Headers.CacheControl!.NoStore.Should().BeTrue(caso);

        using JsonDocument corpo = await LerAsync(resposta);
        corpo.RootElement.GetProperty("nome").GetString().Should().Be("Medicina 2027", caso);
        corpo.RootElement.GetProperty("aviso").GetString().Should().Be(
            "A lista de publicações deste processo seletivo pode estar incompleta no momento. "
            + "Tente novamente em alguns minutos.",
            caso);
    }

    [Fact(DisplayName = "Página completa: selo do conteúdo composto, 304 quando o cliente já o tem")]
    public async Task PaginaCompleta_RevalidaPorSelo()
    {
        ServirCertame();
        _factory.Registro.Resposta = new LeituraDosAtos(AtosEmOrdemDaOrigem(), Falha: null);

        using HttpClient client = _factory.CreateClient();
        using HttpResponseMessage primeira = await client.GetAsync(new Uri(Rota, UriKind.Relative));
        EntityTagHeaderValue selo = primeira.Headers.ETag!;

        using HttpRequestMessage condicional = new(HttpMethod.Get, new Uri(Rota, UriKind.Relative));
        condicional.Headers.IfNoneMatch.Add(selo);
        using HttpResponseMessage segunda = await client.SendAsync(condicional);

        segunda.StatusCode.Should().Be(HttpStatusCode.NotModified);
        segunda.Headers.ETag.Should().Be(selo);
    }

    [Fact(DisplayName = "Ato novo em Publicações muda o selo, embora o selo de Seleção não mude")]
    public async Task AtoNovo_MudaOSelo()
    {
        ServirCertame();
        _factory.Registro.Resposta = new LeituraDosAtos([AtoCriador()], Falha: null);

        using HttpClient client = _factory.CreateClient();
        using HttpResponseMessage antes = await client.GetAsync(new Uri(Rota, UriKind.Relative));
        EntityTagHeaderValue seloAntes = antes.Headers.ETag!;

        _factory.Registro.Resposta = new LeituraDosAtos(AtosEmOrdemDaOrigem(), Falha: null);
        using HttpRequestMessage condicional = new(HttpMethod.Get, new Uri(Rota, UriKind.Relative));
        condicional.Headers.IfNoneMatch.Add(seloAntes);
        using HttpResponseMessage depois = await client.SendAsync(condicional);

        depois.StatusCode.Should().Be(HttpStatusCode.OK);
        depois.Headers.ETag.Should().NotBe(seloAntes);
    }

    [Fact(DisplayName = "Linha do tempo degradada não emite selo")]
    public async Task LinhaDoTempoDegradada_SemSelo()
    {
        ServirCertame();
        _factory.Registro.Resposta = new LeituraDosAtos([], FalhaNaLeituraDosAtos.OrigemIndisponivel);

        using HttpClient client = _factory.CreateClient();
        using HttpResponseMessage resposta = await client.GetAsync(new Uri(Rota, UriKind.Relative));

        resposta.Headers.ETag.Should().BeNull();
        using JsonDocument corpo = await LerAsync(resposta);
        corpo.RootElement.GetProperty("dataPublicacao").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact(DisplayName = "Versão do tipo de mídia não suportada: 406")]
    public async Task VersaoNaoSuportada_406()
    {
        ServirCertame();

        using HttpClient client = _factory.CreateClient();
        using HttpRequestMessage pedido = new(HttpMethod.Get, new Uri(Rota, UriKind.Relative));
        pedido.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.uniplus.certame-publico.v9+json"));

        using HttpResponseMessage resposta = await client.SendAsync(pedido);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
    }

    private void ServirCertame() =>
        _factory.Contrato.Resposta = new ConsultaDoCertame.Encontrado(
            new CertameDaOrigem(ProcessoId, CriadorId, "Medicina 2027", "012/2026", EditalNoAcervo, new Dictionary<Guid, Uri>(), SituacaoDaInscricao.UltimosDias));

    /// <summary>Ordem da origem: do mais antigo para o mais recente.</summary>
    private static AtoDaOrigem[] AtosEmOrdemDaOrigem() =>
    [
        AtoCriador(),
        AtoAviso(),
        new(ResultadoId, "RESULTADO_FINAL", "Resultado final", 2027, "003/2027", new DateOnly(2027, 3, 10), false, null),
    ];

    private static AtoDaOrigem AtoCriador() =>
        new(CriadorId, "EDITAL_ABERTURA", "Edital de abertura", 2026, "012/2026", new DateOnly(2026, 12, 1), true, null);

    private static AtoDaOrigem AtoAviso() =>
        new(AvisoId, "AVISO", null, 2027, null, new DateOnly(2027, 1, 15), false, null);

    private static AtoDaOrigem AtoRetificacaoMaisRecente() =>
        new(
            Guid.Parse("0199a000-0000-7000-8000-00000000000d"),
            "RETIFICACAO",
            "Retificação",
            2027,
            "001/2027",
            new DateOnly(2027, 2, 1),
            true,
            "Ajuste do cronograma");

    private static async Task<JsonDocument> LerAsync(HttpResponseMessage resposta) =>
        await JsonDocument.ParseAsync(await resposta.Content.ReadAsStreamAsync());
}
