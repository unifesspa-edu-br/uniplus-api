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
/// As opções que o processo declara para um fato de fonte do processo, como as edições do ENEM
/// admitidas, voltam na leitura do processo, mesmo que nenhum formulário colete o fato ainda.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class OpcoesDeclaradasEndpointTests
{
    private readonly CascadingFixture _fixture;

    public OpcoesDeclaradasEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "GET /processos-seletivos/{id} devolve as edições do ENEM declaradas, na ordem, sem o formulário coletá-las")]
    public async Task Obter_EdicoesDeclaradas_VoltamNaLeitura()
    {
        Guid processoId = await SemearProcessoDoEnemAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpRequestMessage itens = Requisicao(HttpMethod.Put, $"/api/selecao/admin/processos-seletivos/{processoId}/formularios/INSCRICAO/itens");
        itens.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        itens.Content = JsonContent.Create(new { itens = Array.Empty<object>(), grupos = Array.Empty<object>() });
        HttpResponseMessage semOCampo = await client.SendAsync(itens);
        semOCampo.StatusCode.Should().Be(HttpStatusCode.NoContent, await semOCampo.Content.ReadAsStringAsync());

        using HttpRequestMessage opcoes = Requisicao(HttpMethod.Put, $"/api/selecao/processos-seletivos/{processoId}/fatos/EDICAO_ENEM/opcoes");
        opcoes.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        opcoes.Content = JsonContent.Create(new
        {
            opcoes = new[] { new { codigo = "2026", rotulo = "ENEM 2026" }, new { codigo = "2025", rotulo = "ENEM 2025" } },
        });
        HttpResponseMessage declaradas = await client.SendAsync(opcoes);
        declaradas.StatusCode.Should().Be(HttpStatusCode.NoContent, await declaradas.Content.ReadAsStringAsync());

        using HttpRequestMessage get = Requisicao(HttpMethod.Get, $"/api/selecao/processos-seletivos/{processoId}");
        get.Headers.Accept.ParseAdd("application/vnd.uniplus.processo-seletivo.v1+json");
        HttpResponseMessage lido = await client.SendAsync(get);
        lido.StatusCode.Should().Be(HttpStatusCode.OK, await lido.Content.ReadAsStringAsync());

        using JsonDocument corpo = JsonDocument.Parse(await lido.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("formularios").EnumerateArray()
            .SelectMany(static f => f.GetProperty("fatosColetados").EnumerateArray())
            .Select(static f => f.GetProperty("fatoCodigo").GetString())
            .Should().NotContain("EDICAO_ENEM");
        JsonElement edicoes = corpo.RootElement.GetProperty("opcoesDeclaradas").EnumerateArray()
            .Single(static o => o.GetProperty("fatoCodigo").GetString() == "EDICAO_ENEM");
        edicoes.GetProperty("opcoes").EnumerateArray()
            .Select(static o => (o.GetProperty("codigo").GetString(), o.GetProperty("rotulo").GetString(), o.GetProperty("ordem").GetInt32()))
            .Should().Equal(("2026", "ENEM 2026", 0), ("2025", "ENEM 2025", 1));
    }

    private async Task<Guid> SemearProcessoDoEnemAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPendenciasSeeder.SemearEnemComCalculoLocalAsync(
            db, $"Opções declaradas {Guid.CreateVersion7()}", ofertaComGrupo: true);
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
