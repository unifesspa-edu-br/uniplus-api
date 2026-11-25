namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDoFormularioPeloEndpoint;

[Collection(CascadingCollection.Name)]
public sealed class FormatoDosTermosEndpointTests(CascadingFixture fixture)
{
    [Fact]
    public async Task Definir_TermoForaDoFormato_Recusa()
    {
        Contexto contexto = await SemearRascunhoAsync(fixture, nameof(Definir_TermoForaDoFormato_Recusa));
        using HttpRequestMessage request = new(HttpMethod.Put, $"/api/selecao/admin/processos-seletivos/{contexto.ProcessoId}/formularios/INSCRICAO/termos");
        request.Content = JsonContent.Create(new { termos = new[] { new { codigo = "termo-a", ordem = 0, termo = new { termoId = Guid.NewGuid(), versaoId = Guid.NewGuid() }, regras = new { obrigatoriedade = new { tipo = "SEMPRE" } } } } });
        Autenticar(request, Autenticacao.PlataformaAdmin);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());

        HttpResponseMessage resposta = await contexto.Client.SendAsync(request);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("uniplus.termo_formulario.codigo_formato_invalido");
    }
}
