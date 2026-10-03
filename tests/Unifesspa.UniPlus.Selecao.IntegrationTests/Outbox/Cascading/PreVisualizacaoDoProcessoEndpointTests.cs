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
/// A pré-visualização do processo pelo HTTP, contra o Postgres: o perfil simulado volta avaliado por
/// formulário, com a lista de documentos, sem gravar nada.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class PreVisualizacaoDoProcessoEndpointTests
{
    private const string MediaType = "application/vnd.uniplus.pre-visualizacao-processo-seletivo.v1+json";

    private readonly CascadingFixture _fixture;

    public PreVisualizacaoDoProcessoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "A pré-visualização devolve o formulário de inscrição avaliado para o perfil; processo inexistente é 404")]
    public async Task PreVisualizar_PerfilSimulado_DevolveOFormularioAvaliado()
    {
        Guid processoId = await SemearProcessoAsync();
        using HttpClient client = _fixture.Factory.CreateClient();
        object estrangeiro = new
        {
            respostas = new Dictionary<string, object> { ["NACIONALIDADE"] = "ESTRANGEIRO" },
        };

        HttpResponseMessage resposta = await EnviarAsync(client, processoId, estrangeiro);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        resposta.Content.Headers.ContentType!.MediaType.Should().Be(MediaType);
        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement rg = corpo.RootElement.GetProperty("formularios").EnumerateArray()
            .Single(static f => f.GetProperty("finalidade").GetString() == "INSCRICAO")
            .GetProperty("itens").EnumerateArray()
            .Single(static i => i.GetProperty("fatoCodigo").GetString() == "RG_NUMERO");
        rg.GetProperty("visivel").GetString().Should().Be("FALSO");
        corpo.RootElement.GetProperty("documentos").ValueKind.Should().Be(JsonValueKind.Array);
        (await EnviarAsync(client, Guid.NewGuid(), estrangeiro)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, Guid processoId, object simulacao)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri($"/api/selecao/processos-seletivos/{processoId}/pre-visualizacao", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Accept.ParseAdd(MediaType);
        request.Content = JsonContent.Create(simulacao);
        return await client.SendAsync(request);
    }

    private async Task<Guid> SemearProcessoAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, $"Pré-visualização {Guid.CreateVersion7()}");
        return processo.Id;
    }
}
