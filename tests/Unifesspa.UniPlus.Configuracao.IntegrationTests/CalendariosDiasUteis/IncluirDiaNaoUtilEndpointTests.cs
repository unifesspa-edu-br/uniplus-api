namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.CalendariosDiasUteis;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Caminho de escrita de <c>POST admin/calendarios-dias-uteis/{id}/dias-nao-uteis</c>
/// (api#1458): CA01–CA11 da issue — inclusão preserva id/versão/datas anteriores,
/// validação de domínio, duplicidade, calendário inexistente, autorização e
/// atomicidade, com Wolverine rodando contra Postgres efêmero.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class IncluirDiaNaoUtilEndpointTests
{
    private const string ColecaoPath = "/api/configuracao/calendarios-dias-uteis";
    private const string AdminPath = "/api/configuracao/admin/calendarios-dias-uteis";

    private readonly ConfiguracaoEndpointFixture _fixture;

    public IncluirDiaNaoUtilEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    // CA02, CA03, CA04: inclui data válida, preserva id/versão e as datas anteriores.
    [Fact(DisplayName = "POST .../dias-nao-uteis com item válido inclui a data e preserva id, versão e datas anteriores")]
    public async Task Incluir_ItemValido_IncluiEPreservaIdentidade()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarDataset(client, VersaoUnica());

        HttpResponseMessage incluir = await EnviarPostAdmin(client, $"{AdminPath}/{id}/dias-nao-uteis", ItemValido());

        incluir.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await incluir.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        root.GetProperty("id").GetGuid().Should().Be(id);
        JsonElement dias = root.GetProperty("diasNaoUteis");
        dias.GetArrayLength().Should().Be(2, "a data original de CriarDataset permanece, e a nova se soma a ela");
        dias.EnumerateArray().Should().Contain(d => d.GetProperty("abrangencia").GetString() == "NACIONAL");
        dias.EnumerateArray().Should().Contain(d =>
            d.GetProperty("abrangencia").GetString() == "ESTADUAL" && d.GetProperty("uf").GetString() == "PA");

        // CA01: consulta subsequente confirma a persistência.
        HttpResponseMessage obter = await client.GetAsync(new Uri($"{ColecaoPath}/{id}", UriKind.Relative));
        using JsonDocument docObter = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        docObter.RootElement.GetProperty("diasNaoUteis").GetArrayLength().Should().Be(2);
    }

    // CA03: não cria outro dataset nem outra versão.
    [Fact(DisplayName = "POST .../dias-nao-uteis não cria um novo calendário — a coleção continua com o mesmo total")]
    public async Task Incluir_ItemValido_NaoCriaNovoCalendario()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarDataset(client, VersaoUnica());
        int totalAntes = await ContarCalendarios();

        HttpResponseMessage incluir = await EnviarPostAdmin(client, $"{AdminPath}/{id}/dias-nao-uteis", ItemValido());
        incluir.StatusCode.Should().Be(HttpStatusCode.OK);

        (await ContarCalendarios()).Should().Be(totalAntes, "a inclusão não deve criar nenhum dataset novo");
    }

    // CA05: recusa dado inválido com erro de domínio, nada persistido.
    [Fact(DisplayName = "POST .../dias-nao-uteis com abrangência inválida retorna 422 e não persiste")]
    public async Task Incluir_AbrangenciaInvalida_Retorna422SemPersistir()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarDataset(client, VersaoUnica());

        HttpResponseMessage incluir = await EnviarPostAdmin(
            client,
            $"{AdminPath}/{id}/dias-nao-uteis",
            new
            {
                abrangencia = "INVALIDO",
                municipioIbge = (string?)null,
                municipioNome = (string?)null,
                municipioUf = (string?)null,
                uf = (string?)null,
                data = "2028-03-01",
                descricao = "Dia qualquer",
            });

        incluir.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await LerCodigoDeErro(incluir)).Should().Be(
            "uniplus.configuracao.calendario_dias_uteis.abrangencia_invalida");

        HttpResponseMessage obter = await client.GetAsync(new Uri($"{ColecaoPath}/{id}", UriKind.Relative));
        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("diasNaoUteis").GetArrayLength().Should().Be(
            1, "a data original de CriarDataset é a única — nada foi incluído");
    }

    // CA06: recusa duplicidade contra data já cadastrada, nada persistido a mais.
    [Fact(DisplayName = "POST .../dias-nao-uteis com combinação já cadastrada retorna 422 e não duplica")]
    public async Task Incluir_CombinacaoJaCadastrada_Retorna422SemDuplicar()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarDataset(client, VersaoUnica());

        HttpResponseMessage incluir = await EnviarPostAdmin(
            client,
            $"{AdminPath}/{id}/dias-nao-uteis",
            new
            {
                abrangencia = "NACIONAL",
                municipioIbge = (string?)null,
                municipioNome = (string?)null,
                municipioUf = (string?)null,
                uf = (string?)null,
                data = "2028-01-01",
                descricao = "Confraternização Universal (duplicata)",
            });

        incluir.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await LerCodigoDeErro(incluir)).Should().Be(
            "uniplus.configuracao.calendario_dias_uteis.data_duplicada_no_dataset");

        HttpResponseMessage obter = await client.GetAsync(new Uri($"{ColecaoPath}/{id}", UriKind.Relative));
        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("diasNaoUteis").GetArrayLength().Should().Be(1);
    }

    // CA07: calendário inexistente retorna 404, nada persistido.
    [Fact(DisplayName = "POST .../dias-nao-uteis em calendário inexistente retorna 404")]
    public async Task Incluir_CalendarioInexistente_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage incluir = await EnviarPostAdmin(
            client, $"{AdminPath}/{Guid.NewGuid()}/dias-nao-uteis", ItemValido());

        incluir.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await LerCodigoDeErro(incluir)).Should().Be(
            "uniplus.configuracao.calendario_dias_uteis.nao_encontrado");
    }

    // CA09: sem role de admin, recusa.
    [Fact(DisplayName = "POST .../dias-nao-uteis sem role plataforma-admin retorna 403")]
    public async Task Incluir_SemRoleAdmin_Retorna403()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarDataset(client, VersaoUnica());

        using HttpRequestMessage request = new(HttpMethod.Post, new Uri($"{AdminPath}/{id}/dias-nao-uteis", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "candidato");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(ItemValido());

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // CA09: sem autenticação, recusa.
    [Fact(DisplayName = "POST .../dias-nao-uteis sem autenticação retorna 401")]
    public async Task Incluir_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(
            HttpMethod.Post, new Uri($"{AdminPath}/{Guid.NewGuid()}/dias-nao-uteis", UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent(JsonSerializer.Serialize(ItemValido()), Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // CA03: a inclusão funciona tanto em calendário vigente quanto em rascunho.
    [Fact(DisplayName = "POST .../dias-nao-uteis inclui em calendário vigente sem alterar a vigência")]
    public async Task Incluir_CalendarioVigente_MantemVigencia()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarDataset(client, VersaoUnica());
        HttpResponseMessage marcarVigente = await EnviarPostAdmin(client, $"{AdminPath}/{id}/vigente", null);
        marcarVigente.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage incluir = await EnviarPostAdmin(client, $"{AdminPath}/{id}/dias-nao-uteis", ItemValido());

        incluir.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await incluir.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("vigente").GetBoolean().Should().BeTrue();
    }

    private static object ItemValido() => new
    {
        abrangencia = "ESTADUAL",
        municipioIbge = (string?)null,
        municipioNome = (string?)null,
        municipioUf = (string?)null,
        uf = "PA",
        data = "2028-08-15",
        descricao = "Adesão do Pará à Independência",
    };

    private static object CorpoValido(string versaoDataset) => new
    {
        versaoDataset,
        diasNaoUteis = new object[]
        {
            new
            {
                abrangencia = "NACIONAL",
                municipioIbge = (string?)null,
                municipioNome = (string?)null,
                municipioUf = (string?)null,
                data = "2028-01-01",
                descricao = "Confraternização Universal",
            },
        },
    };

    private static string VersaoUnica() => $"inc-{Guid.NewGuid():N}"[..20];

    private static async Task<Guid> CriarDataset(HttpClient client, string versaoDataset)
    {
        HttpResponseMessage criar = await EnviarPostAdmin(client, AdminPath, CorpoValido(versaoDataset));
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        return await criar.Content.ReadFromJsonAsync<Guid>();
    }

    private async Task<int> ContarCalendarios()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        ConfiguracaoDbContext db = scope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        return await db.CalendariosDiasUteis.CountAsync();
    }

    private static async Task<string?> LerCodigoDeErro(HttpResponseMessage response)
    {
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("code").GetString();
    }

    private static async Task<HttpResponseMessage> EnviarPostAdmin(HttpClient client, string path, object? body)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(path, UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
}
