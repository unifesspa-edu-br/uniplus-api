namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// A UF e o município de nascimento pelo HTTP, contra o Postgres (UNI-REQ-0145): o formulário de
/// inscrição os coleta na seção do conjunto básico, e o município, no campo de município, cita a
/// UF respondida antes.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class CampoDeMunicipioEndpointTests
{
    private readonly CascadingFixture _fixture;

    public CampoDeMunicipioEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "O PUT dos itens da inscrição grava a seção do conjunto básico, e o GET devolve o município com a UF citada")]
    public async Task DefinirItens_Inscricao_GravaOMunicipioDeNascimento()
    {
        Guid processoId = await SemearProcessoAsync();

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage put = Requisicao(HttpMethod.Put, $"/api/selecao/admin/processos-seletivos/{processoId}/formularios/INSCRICAO/itens");
        put.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        put.Content = JsonContent.Create(new { itens = Array.Empty<object>(), grupos = Array.Empty<object>() });
        HttpResponseMessage gravado = await client.SendAsync(put);
        gravado.StatusCode.Should().Be(HttpStatusCode.NoContent, await gravado.Content.ReadAsStringAsync());

        using HttpRequestMessage get = Requisicao(HttpMethod.Get, $"/api/selecao/processos-seletivos/{processoId}");
        get.Headers.Accept.ParseAdd("application/vnd.uniplus.processo-seletivo.v1+json");
        HttpResponseMessage lido = await client.SendAsync(get);
        lido.StatusCode.Should().Be(HttpStatusCode.OK, await lido.Content.ReadAsStringAsync());

        using JsonDocument corpo = JsonDocument.Parse(await lido.Content.ReadAsStringAsync());
        JsonElement municipio = corpo.RootElement.GetProperty("formularios").EnumerateArray()
            .Single(static f => f.GetProperty("finalidade").GetString() == "INSCRICAO")
            .GetProperty("fatosColetados").EnumerateArray()
            .Single(static f => f.GetProperty("fatoCodigo").GetString() == "NATURALIDADE_MUNICIPIO");
        municipio.GetProperty("etapaCodigo").GetString().Should().Be("DADOS_BASICOS");
        municipio.GetProperty("tipoRenderizacao").GetString().Should().Be("MUNICIPIO");
        JsonElement restricao = municipio.GetProperty("restricoes").EnumerateArray().Single();
        restricao.GetProperty("tipo").GetString().Should().Be("MUNICIPIOS_DA_UF");
        restricao.GetProperty("fatos").EnumerateArray().Single().GetString().Should().Be("NATURALIDADE_UF");
    }

    private async Task<Guid> SemearProcessoAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, $"Campo de município {Guid.CreateVersion7()}");
        return processo.Id;
    }

    private static HttpRequestMessage Requisicao(HttpMethod metodo, string rota)
    {
        HttpRequestMessage request = new(metodo, new Uri(rota, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        return request;
    }
}
