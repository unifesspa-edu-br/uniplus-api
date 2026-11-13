namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// O instante que o cliente informa na query do snapshot vigente chega ao handler já em microssegundos, a precisão
/// que o banco persiste. A mensagem do 422 cita o instante com que a consulta foi feita, e é por ela que se vê o
/// valor recebido pelo handler.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class InstanteDaQueryDoSnapshotVigenteEndpointTests
{
    private readonly CascadingFixture _fixture;

    public InstanteDaQueryDoSnapshotVigenteEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Instante da query com 7 casas é consultado truncado em microssegundos")]
    public async Task Instante_SetimaCasa_ConsultadoEmMicrossegundos()
    {
        Guid processoId = await SemearRascunhoAsync();

        using HttpResponseMessage resposta = await ConsultarAsync(processoId, "instante=2026-11-12T10:00:00.9999999Z");

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        string detalhe = await DetalheAsync(resposta);
        detalhe.Should().Contain("2026-11-12T10:00:00.9999990+00:00").And.NotContain("9999999");
    }

    [Fact(DisplayName = "Instante da query com offset diferente de UTC é consultado truncado e com o mesmo offset")]
    public async Task Instante_ComOffset_ConsultadoEmMicrossegundosComOMesmoOffset()
    {
        Guid processoId = await SemearRascunhoAsync();

        using HttpResponseMessage resposta = await ConsultarAsync(processoId, "instante=2026-11-12T10:00:00.1234567-03:00");

        (await DetalheAsync(resposta)).Should().Contain("2026-11-12T10:00:00.1234560-03:00");
    }

    [Fact(DisplayName = "Instante da query que não é data e hora é recusado como valor inválido")]
    public async Task Instante_Invalido_ERecusadoComoValorInvalido()
    {
        Guid processoId = await SemearRascunhoAsync();

        using HttpResponseMessage resposta = await ConsultarAsync(processoId, "instante=amanha-de-manha");

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("code").GetString().Should().Be("uniplus.requisicao.valor_invalido");
    }

    private async Task<Guid> SemearRascunhoAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, $"Instante em microssegundos {Guid.CreateVersion7()}");

        return processo.Id;
    }

    private async Task<HttpResponseMessage> ConsultarAsync(Guid processoId, string query)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage requisicao = new(
            HttpMethod.Get, new Uri($"/api/selecao/processos-seletivos/{processoId}/snapshot-vigente?{query}", UriKind.Relative));
        requisicao.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        requisicao.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");

        return await client.SendAsync(requisicao);
    }

    private static async Task<string> DetalheAsync(HttpResponseMessage resposta)
    {
        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());

        return corpo.RootElement.GetProperty("detail").GetString()!;
    }
}
