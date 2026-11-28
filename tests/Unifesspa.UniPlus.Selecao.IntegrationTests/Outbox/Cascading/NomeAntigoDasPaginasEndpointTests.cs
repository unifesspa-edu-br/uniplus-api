namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDoFormularioPeloEndpoint;

[Collection(CascadingCollection.Name)]
public sealed class NomeAntigoDasPaginasEndpointTests(CascadingFixture fixture)
{
    [Fact]
    public async Task Definir_ComEtapasNoLugarDePaginas_RecusaAListaDePaginasAusente()
    {
        Contexto contexto = await SemearRascunhoAsync(fixture, nameof(Definir_ComEtapasNoLugarDePaginas_RecusaAListaDePaginasAusente));
        using HttpRequestMessage pedido = new(HttpMethod.Put,
            new Uri($"/api/selecao/admin/processos-seletivos/{contexto.ProcessoId}/formularios/INSCRICAO", UriKind.Relative))
        {
            Content = JsonContent.Create(new { titulo = "Inscrição", etapas = PaginasDoFormulario() }),
        };
        Autenticar(pedido, Autenticacao.PlataformaAdmin);
        pedido.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
        using HttpResponseMessage resposta = await contexto.Client.SendAsync(pedido);
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("errors").EnumerateArray().Select(static e => e.GetProperty("field").GetString())
            .Should().Equal("obrigatorio", "paginas");
    }
}
