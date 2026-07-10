namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.AtosNormativos;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

/// <summary>
/// Endpoints de <c>AtoNormativo</c> contra o monólito real com Postgres efêmero:
/// registro, autorização, idempotência, resolução do tipo vigente, cópia por
/// valor, aviso de numeração (AC4) e o append-only exposto por HTTP.
/// </summary>
[Collection(PublicacoesEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class AtoNormativoEndpointTests
{
    private const string BaseAtos = "/api/publicacoes/atos";
    private const string AdminAtos = "/api/publicacoes/admin/atos";
    private const string AdminTipos = "/api/publicacoes/admin/tipos-ato";
    private const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string Publicacao = "2026-03-13";

    private readonly PublicacoesEndpointFixture _fixture;

    public AtoNormativoEndpointTests(PublicacoesEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    // ── Leitura pública ──────────────────────────────────────────────────────

    [Fact(DisplayName = "GET atos retorna 200 com Content-Type vendor MIME")]
    public async Task Listar_Retorna200ComVendorMime()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri(BaseAtos, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.ato-normativo.v1+json");
    }

    [Fact(DisplayName = "GET atos/{id} retorna 404 quando inexistente")]
    public async Task ObterPorId_NaoExiste_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"{BaseAtos}/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Autorização ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "POST admin sem autenticação retorna 401")]
    public async Task Registrar_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(AdminAtos, UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST admin sem role plataforma-admin retorna 403")]
    public async Task Registrar_SemRoleAdmin_Retorna403()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(AdminAtos, UriKind.Relative));
        Autenticar(request, role: "candidato");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(PayloadAto("EDITAL_ABERTURA", "EDITAL", "1"));

        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Idempotência ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "POST sem Idempotency-Key retorna 400")]
    public async Task Registrar_SemIdempotencyKey_Retorna400()
    {
        string tipo = await CriarTipoVigenteAsync();

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(AdminAtos, UriKind.Relative));
        Autenticar(request);
        request.Content = JsonContent.Create(PayloadAto(tipo, SerieUnica(), "1"));

        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "POST repetido com a mesma chave e o mesmo corpo devolve a resposta cacheada")]
    public async Task Registrar_ReplayLegitimo_DevolveCache()
    {
        string tipo = await CriarTipoVigenteAsync();
        object payload = PayloadAto(tipo, SerieUnica(), "1");
        string chave = Guid.NewGuid().ToString();

        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage primeira = await PostAtoAsync(client, payload, chave);
        primeira.StatusCode.Should().Be(HttpStatusCode.Created);
        string corpoPrimeira = await primeira.Content.ReadAsStringAsync();

        HttpResponseMessage segunda = await PostAtoAsync(client, payload, chave);
        segunda.StatusCode.Should().Be(HttpStatusCode.Created);
        (await segunda.Content.ReadAsStringAsync()).Should().Be(corpoPrimeira);
    }

    // ── Registro ─────────────────────────────────────────────────────────────

    [Fact(DisplayName = "POST registra, devolve 201 com Location e copia congela/efeito do catálogo")]
    public async Task Registrar_Retorna201_CopiaConsequencia()
    {
        string tipo = await CriarTipoVigenteAsync(congela: true, efeito: true);

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await PostAtoAsync(client, PayloadAto(tipo, SerieUnica(), "13"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.AbsolutePath.Should().StartWith($"{BaseAtos}/");

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Guid atoId = doc.RootElement.GetProperty("atoId").GetGuid();
        doc.RootElement.GetProperty("registradoEm").GetDateTimeOffset().Should().NotBe(default);

        // A consequência gravada é a do catálogo vigente, copiada por valor (AC5).
        using JsonDocument detalhe = JsonDocument.Parse(
            await client.GetStringAsync(new Uri($"{BaseAtos}/{atoId}", UriKind.Relative)));
        detalhe.RootElement.GetProperty("congelaConfiguracao").GetBoolean().Should().BeTrue();
        detalhe.RootElement.GetProperty("efeitoIrreversivel").GetBoolean().Should().BeTrue();
        detalhe.RootElement.GetProperty("_links").GetProperty("self").GetString()
            .Should().Be($"{BaseAtos}/{atoId}");
    }

    [Fact(DisplayName = "POST de ato sem número é aceito (número é opcional)")]
    public async Task Registrar_SemNumero_Retorna201()
    {
        string tipo = await CriarTipoVigenteAsync();

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await PostAtoAsync(client, PayloadAto(tipo, SerieUnica(), numero: null));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact(DisplayName = "POST de tipo sem versão vigente na data retorna 422 nomeado")]
    public async Task Registrar_TipoSemVigencia_Retorna422()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        // Tipo nunca cadastrado: não há versão vigente para copiar.
        HttpResponseMessage response = await PostAtoAsync(
            client, PayloadAto("TIPO_INEXISTENTE_" + Sufixo(), SerieUnica(), "1"));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync())
            .Should().Contain("uniplus.publicacoes.ato_normativo.tipo_sem_versao_vigente");
    }

    [Fact(DisplayName = "POST com par de versão incompleto retorna 422")]
    public async Task Registrar_VersaoInvocadaIncompleta_Retorna422()
    {
        string tipo = await CriarTipoVigenteAsync();

        object payload = new
        {
            orgao = "CEPS",
            serie = SerieUnica(),
            ano = 2026,
            numero = "1",
            tipoCodigo = tipo,
            dataPublicacao = Publicacao,
            documentoHash = HashValido,
            assinante = "Jairo Belchior",
            versaoInvocadaId = Guid.CreateVersion7(),
            versaoInvocadaHash = (string?)null,
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await PostAtoAsync(client, payload);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "POST com id de versão zerado (Guid.Empty) retorna 422, não 500")]
    public async Task Registrar_VersaoInvocadaIdVazio_Retorna422()
    {
        string tipo = await CriarTipoVigenteAsync();

        object payload = new
        {
            orgao = "CEPS",
            serie = SerieUnica(),
            ano = 2026,
            numero = "1",
            tipoCodigo = tipo,
            dataPublicacao = Publicacao,
            documentoHash = HashValido,
            assinante = "Jairo Belchior",
            versaoInvocadaId = Guid.Empty,
            versaoInvocadaHash = HashValido,
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await PostAtoAsync(client, payload);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── Aviso de numeração (AC4) ─────────────────────────────────────────────

    [Fact(DisplayName = "Número já usado gera aviso no registro, sem impedir; detalhe recomputa")]
    public async Task Registrar_NumeroDuplicado_Avisa()
    {
        string tipo = await CriarTipoVigenteAsync();
        string serie = SerieUnica();

        using HttpClient client = _fixture.Factory.CreateClient();

        // Primeiro ato: sem conflito.
        HttpResponseMessage primeira = await PostAtoAsync(client, PayloadAto(tipo, serie, "13"));
        primeira.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid primeiroId = JsonDocument.Parse(await primeira.Content.ReadAsStringAsync())
            .RootElement.GetProperty("atoId").GetGuid();

        // Segundo ato, mesma numeração: aceito, com aviso.
        HttpResponseMessage segunda = await PostAtoAsync(client, PayloadAto(tipo, serie, "13"));
        segunda.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument doc = JsonDocument.Parse(await segunda.Content.ReadAsStringAsync());
        JsonElement avisos = doc.RootElement.GetProperty("avisos");
        avisos.GetArrayLength().Should().Be(1);
        avisos[0].GetProperty("codigo").GetString().Should().Be("NumeroDuplicado");
        Guid segundoId = doc.RootElement.GetProperty("atoId").GetGuid();

        // O detalhe do segundo ato recomputa o aviso, apontando o primeiro.
        using JsonDocument detalhe = JsonDocument.Parse(
            await client.GetStringAsync(new Uri($"{BaseAtos}/{segundoId}", UriKind.Relative)));
        JsonElement conflitantes = detalhe.RootElement
            .GetProperty("avisos")[0].GetProperty("atosConflitantes");
        conflitantes.EnumerateArray().Select(e => e.GetGuid()).Should().Contain(primeiroId);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void Autenticar(HttpRequestMessage request, string role = "plataforma-admin")
    {
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, role);
    }

    private static object PayloadAto(string tipoCodigo, string serie, string? numero) =>
        new
        {
            orgao = "CEPS",
            serie,
            ano = 2026,
            numero,
            tipoCodigo,
            dataPublicacao = Publicacao,
            documentoHash = HashValido,
            assinante = "Jairo Belchior",
        };

    private static async Task<HttpResponseMessage> PostAtoAsync(
        HttpClient client, object payload, string? chave = null)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(AdminAtos, UriKind.Relative));
        Autenticar(request);
        request.Headers.Add("Idempotency-Key", chave ?? Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(payload);

        return await client.SendAsync(request);
    }

    /// <summary>Cadastra um tipo de ato vigente (janela ampla) e devolve o código.</summary>
    private async Task<string> CriarTipoVigenteAsync(bool congela = false, bool efeito = false)
    {
        string codigo = "ATO_TIPO_" + Sufixo();
        object payload = new
        {
            codigo,
            nome = "Tipo para ato de teste",
            congelaConfiguracao = congela,
            unicoPorObjeto = false,
            efeitoIrreversivel = efeito,
            vigenciaInicio = "2020-01-01",
            vigenciaFim = (string?)null,
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(AdminTipos, UriKind.Relative));
        Autenticar(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(payload);

        HttpResponseMessage response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return codigo;
    }

    /// <summary>Série única no teste, para isolar a numeração do aviso entre casos.</summary>
    private static string SerieUnica() => "EDITAL_" + Sufixo();

    private static string Sufixo()
    {
        string hex = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12];
        return string.Concat(hex.Select(c => (char)('A' + Convert.ToInt32(c.ToString(), 16))));
    }
}
