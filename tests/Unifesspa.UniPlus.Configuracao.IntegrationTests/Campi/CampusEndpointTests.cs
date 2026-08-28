namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Campi;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Smoke + caminho de escrita dos endpoints de <c>Campus</c> (UNI-REQ #587):
/// routing, vendor media type, HATEOAS, autenticação/autorização, idempotência e
/// validação de formato da cidade (CA-03) com Wolverine rodando contra Postgres
/// efêmero.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class CampusEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public CampusEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /api/configuracao/campi retorna 200 com Content-Type vendor MIME de campus")]
    public async Task Listar_Retorna200ComVendorMime()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/api/configuracao/campi", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.campus.v1+json");
    }

    [Fact(DisplayName = "GET /api/configuracao/campi/{id} retorna 404 quando inexistente")]
    public async Task ObterPorId_NaoExiste_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/configuracao/campi/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "POST /api/configuracao/admin/campi sem autenticação retorna 401")]
    public async Task Criar_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/campi", UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST /api/configuracao/admin/campi autenticado sem role plataforma-admin retorna 403")]
    public async Task Criar_SemRoleAdmin_Retorna403()
    {
        var body = new
        {
            sigla = $"C{Guid.NewGuid().ToString("N")[..6]}",
            nome = "Campus Sem Permissão",
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/campi", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "candidato"); // role insuficiente
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a policy [Authorize(Roles = \"plataforma-admin\")] nega um principal autenticado sem o role");
    }

    [Fact(DisplayName = "POST /api/configuracao/admin/campi sem Idempotency-Key retorna 400")]
    public async Task Criar_SemIdempotencyKey_Retorna400()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/campi", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "POST /api/configuracao/admin/campi cria (201) e o GET subsequente retorna a cidade por código + display cache")]
    public async Task Criar_ComAuthEIdempotency_Retorna201EPersisteCidade()
    {
        string sigla = $"C{Guid.NewGuid().ToString("N")[..6]}";
        var body = new
        {
            sigla,
            nome = "Campus de Teste",
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, "/api/configuracao/admin/campi", body);

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        id.Should().NotBe(Guid.Empty);

        HttpResponseMessage obter = await client.GetAsync(new Uri($"/api/configuracao/campi/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        JsonElement cidade = root.GetProperty("cidade");
        cidade.GetProperty("codigoIbge").GetString().Should().Be("1504208");
        cidade.GetProperty("nome").GetString().Should().Be("Marabá");
        cidade.GetProperty("uf").GetString().Should().Be("PA");
        cidade.GetProperty("origem").GetString().Should().Be("geo-api");
        root.TryGetProperty("_links", out _).Should().BeTrue("HATEOAS Level 1 expõe _links.self (ADR-0029)");
    }

    [Fact(DisplayName = "CA-02: POST com endereço aninhado retorna endereco estruturado no GET")]
    public async Task Criar_ComEnderecoAninhado_RetornaEnderecoNoGet()
    {
        string sigla = $"C{Guid.NewGuid().ToString("N")[..6]}";
        var body = new
        {
            sigla,
            nome = "Campus com Endereço",
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
            endereco = new
            {
                cep = "68507590",
                logradouro = "Folha 31, Quadra 7",
                numero = "s/n",
                bairro = "Nova Marabá",
                cidade = new { codigoIbge = "1504208", nome = "Marabá", uf = "PA" },
                latitude = -5.368m,
                longitude = -49.118m,
                nivelResolucao = "logradouro",
                origem = "logradouro",
            },
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, "/api/configuracao/admin/campi", body);
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage obter = await client.GetAsync(new Uri($"/api/configuracao/campi/{id}", UriKind.Relative));
        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        JsonElement endereco = doc.RootElement.GetProperty("endereco");
        endereco.GetProperty("cep").GetString().Should().Be("68507590");
        endereco.GetProperty("logradouro").GetString().Should().Be("Folha 31, Quadra 7");
        endereco.GetProperty("cidade").GetProperty("codigoIbge").GetString().Should().Be("1504208");
        endereco.GetProperty("nivelResolucao").GetString().Should().Be("logradouro");
    }

    [Fact(DisplayName = "CA-04: POST com endereço de cidade incoerente retorna 422")]
    public async Task Criar_EnderecoCidadeIncoerente_Retorna422()
    {
        var body = new
        {
            sigla = $"C{Guid.NewGuid().ToString("N")[..6]}",
            nome = "Campus Incoerente",
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
            endereco = new
            {
                cep = "66000000",
                cidade = new { codigoIbge = "1501402", nome = "Belém", uf = "PA" },
                nivelResolucao = "cidade",
                origem = "faixa-cidade",
            },
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, "/api/configuracao/admin/campi", body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "CA-03: POST /api/configuracao/admin/campi com código IBGE malformado retorna 422 sem consultar o Geo")]
    public async Task Criar_CidadeMalformada_Retorna422()
    {
        var body = new
        {
            sigla = $"C{Guid.NewGuid().ToString("N")[..6]}",
            nome = "Campus Inválido",
            cidadeCodigoIbge = "150420", // 6 dígitos
            cidadeNome = "Marabá",
            cidadeUf = "PA",
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, "/api/configuracao/admin/campi", body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "CA-03: POST com CEP em formato inválido no endereço estruturado retorna 422")]
    public async Task Criar_CepInvalido_Retorna422()
    {
        var body = new
        {
            sigla = $"C{Guid.NewGuid().ToString("N")[..6]}",
            nome = "Campus CEP Inválido",
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
            endereco = new
            {
                cep = "123", // formato inválido — não são 8 dígitos
                cidade = new { codigoIbge = "1504208", nome = "Marabá", uf = "PA" },
                nivelResolucao = "cidade",
                origem = "faixa-cidade",
            },
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, "/api/configuracao/admin/campi", body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "ADR-0125: POST com Sigla e Nome vazios ao mesmo tempo retorna as duas violações em errors[], campo em camelCase")]
    public async Task Criar_SiglaENomeVazios_DeveConterAsDuasViolacoesEmErrors()
    {
        var body = new
        {
            sigla = "",
            nome = "",
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, "/api/configuracao/admin/campi", body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.configuracao.campus.sigla_obrigatoria");

        // field usa o mesmo casing do payload JSON (camelCase, ADR-0023 — "caminho
        // dot-notation"), não o PascalCase do C#.
        JsonElement errors = doc.RootElement.GetProperty("errors");
        errors.GetArrayLength().Should().Be(2);
        errors[0].GetProperty("field").GetString().Should().Be("sigla");
        errors[0].GetProperty("code").GetString().Should().Be("uniplus.configuracao.campus.sigla_obrigatoria");
        errors[1].GetProperty("field").GetString().Should().Be("nome");
        errors[1].GetProperty("code").GetString().Should().Be("uniplus.configuracao.campus.nome_obrigatorio");
    }

    /// <summary>
    /// ADR-0125: prova que o campo <c>sigla</c> genuinamente ausente do JSON (não
    /// só uma string vazia) chega ao domínio como violação específica, em vez de
    /// ser interceptado pelo model binding automático do <c>[ApiController]</c>
    /// (que rejeitaria com um 400 genérico do ASP.NET, fora do formato RFC 9457).
    /// Só é possível porque
    /// <see cref="Application.Commands.Campi.CriarCampusCommand.Sigla"/> é
    /// <c>string?</c>, não <c>string</c>.
    /// </summary>
    [Fact(DisplayName = "ADR-0125: POST com sigla genuinamente ausente do JSON (não string vazia) chega ao domínio como 422 específico")]
    public async Task Criar_SiglaAusenteDoJson_ChegaAoDominioComoViolacaoEspecifica()
    {
        // Corpo cru — "sigla" nem existe como chave, diferente de sigla: "".
        const string json = """{"nome":"Campus Teste","cidadeCodigoIbge":"1504208","cidadeNome":"Marabá","cidadeUf":"PA"}""";

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/campi", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.configuracao.campus.sigla_obrigatoria");

        // ADR-0023: errors[] é condicional a status 422, não a haver mais de uma
        // violação — uma única Sigla ausente também carrega o array, com 1 elemento.
        JsonElement errors = doc.RootElement.GetProperty("errors");
        errors.GetArrayLength().Should().Be(1);
        errors[0].GetProperty("field").GetString().Should().Be("sigla");
        errors[0].GetProperty("code").GetString().Should().Be("uniplus.configuracao.campus.sigla_obrigatoria");
    }

    [Fact(DisplayName = "POST com sigla acentuada retorna 422 com o código público de acentuação no campo sigla")]
    public async Task Criar_SiglaAcentuada_Retorna422ComCodigoPublico()
    {
        // CA-07: recusa associada ao campo sigla, com código de domínio próprio.
        const string siglaAcentuada = "CÁMAR";
        var body = new
        {
            sigla = siglaAcentuada,
            nome = "Campus com Acento",
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, "/api/configuracao/admin/campi", body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        string payload = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(payload);
        doc.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.configuracao.campus.sigla_acentuacao_invalida");

        JsonElement errors = doc.RootElement.GetProperty("errors");
        errors.GetArrayLength().Should().Be(1);
        errors[0].GetProperty("field").GetString().Should().Be("sigla");
        errors[0].GetProperty("code").GetString()
            .Should().Be("uniplus.configuracao.campus.sigla_acentuacao_invalida");

        // CA-10: nem o detail da raiz nem a message do item devolvem o valor rejeitado.
        doc.RootElement.GetProperty("detail").GetString().Should().NotContain(siglaAcentuada);
        errors[0].GetProperty("message").GetString().Should().NotContain(siglaAcentuada);
    }

    [Fact(DisplayName = "POST com sigla cujo acento é marca combinante retorna o mesmo código de acentuação")]
    public async Task Criar_SiglaComMarcaCombinante_RetornaMesmoCodigo()
    {
        // CA-03: "CÁMAR" em NFD trafega no JSON como 'A' + U+0301 e precisa cair na
        // mesma regra do pré-composto.
        var body = new
        {
            sigla = "CA\u0301MAR",
            nome = "Campus com Acento Combinante",
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, "/api/configuracao/admin/campi", body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.configuracao.campus.sigla_acentuacao_invalida");
    }

    [Fact(DisplayName = "POST com sigla acentuada e nome vazio devolve as duas violações em errors[]")]
    public async Task Criar_SiglaAcentuadaENomeVazio_DevolveAsDuasViolacoes()
    {
        // CA-08 sobre a ADR-0125: a acentuação entra no mesmo lote das demais.
        var body = new
        {
            sigla = "CÁMAR",
            nome = "",
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, "/api/configuracao/admin/campi", body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement errors = doc.RootElement.GetProperty("errors");
        errors.GetArrayLength().Should().Be(2);
        errors[0].GetProperty("field").GetString().Should().Be("sigla");
        errors[0].GetProperty("code").GetString()
            .Should().Be("uniplus.configuracao.campus.sigla_acentuacao_invalida");
        errors[1].GetProperty("field").GetString().Should().Be("nome");
        errors[1].GetProperty("code").GetString()
            .Should().Be("uniplus.configuracao.campus.nome_obrigatorio");
    }

    [Fact(DisplayName = "PUT que introduz acentuação na sigla retorna 422 e o GET seguinte mantém a sigla anterior")]
    public async Task Atualizar_IntroduzAcentuacaoNaSigla_Retorna422EPreservaSigla()
    {
        // CA-02: a atualização aplica a mesma validação, e o agregado rastreado pelo
        // EF não pode ter sido mutado antes da recusa.
        string sigla = $"C{Guid.NewGuid().ToString("N")[..6]}".ToUpperInvariant();
        var criacao = new
        {
            sigla,
            nome = "Campus para Atualizar",
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, "/api/configuracao/admin/campi", criacao);
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        var atualizacao = new
        {
            id,
            sigla = $"{sigla[..^1]}Á",
            nome = "Campus para Atualizar",
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
        };

        using HttpRequestMessage request = new(
            HttpMethod.Put, new Uri($"/api/configuracao/admin/campi/{id}", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(atualizacao);

        HttpResponseMessage atualizar = await client.SendAsync(request);

        atualizar.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using JsonDocument problema = JsonDocument.Parse(await atualizar.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.configuracao.campus.sigla_acentuacao_invalida");
        problema.RootElement.GetProperty("errors")[0].GetProperty("field").GetString().Should().Be("sigla");

        HttpResponseMessage obter = await client.GetAsync(new Uri($"/api/configuracao/campi/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("sigla").GetString().Should().Be(sigla,
            "a recusa não pode ter persistido a sigla acentuada");
    }

    private static async Task<HttpResponseMessage> EnviarPostAdmin(HttpClient client, string path, object body)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(path, UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
}
