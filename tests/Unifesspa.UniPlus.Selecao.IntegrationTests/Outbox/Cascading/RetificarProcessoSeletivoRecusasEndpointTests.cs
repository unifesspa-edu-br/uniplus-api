namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.AcessoAosDadosDoProcesso;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDaPublicacaoDoProcessoSeletivo;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDasRecusasDoDocumentoEDoTipoNoEdital;

/// <summary>
/// O <c>POST /processos-seletivos/{id}/retificacoes</c> com o documento ainda não confirmado e o tipo do ato que o catálogo não tem: as duas
/// recusas vêm juntas, cada uma no seu campo, e o acesso ao processo é conferido antes de qualquer conferência do pedido.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class RetificarProcessoSeletivoRecusasEndpointTests
{
    private readonly CascadingFixture _fixture;

    public RetificarProcessoSeletivoRecusasEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName =
        "POST /processos-seletivos/{id}/retificacoes com documento não confirmado e tipo do ato sem versão vigente — 422 com as duas recusas, cada uma no seu campo, e nada gravado")]
    public async Task Retificar_QuandoDocumentoEOTipoSaoRecusados_Retorna422ComAsDuasRecusas()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient client = api.CreateClient();
        Guid processoId = await SemearPublicadoAsync(api, client, nameof(Retificar_QuandoDocumentoEOTipoSaoRecusados_Retorna422ComAsDuasRecusas));
        Guid documentoPendente = await SemearDocumentoPendenteAsync(api, processoId);

        using HttpResponseMessage response = await PostRetificacaoAsync(client, processoId, CorpoDaRetificacao(documentoPendente));

        await ConferirAsync(response);
        (await ContarVersoesAsync(api, processoId)).Should().Be(1, "a recusa não deixou nada gravado");
    }

    [Fact(DisplayName =
        "POST /processos-seletivos/{id}/retificacoes sem permissão no processo — 404 com o código de processo não encontrado, mesmo com o documento e o tipo recusados")]
    public async Task Retificar_SemPermissaoComTodasAsRecusas_Retorna404AntesDeTudo()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient client = api.CreateClient();
        Guid processoId = await SemearPublicadoAsync(api, client, nameof(Retificar_SemPermissaoComTodasAsRecusas_Retorna404AntesDeTudo));
        Guid documentoPendente = await SemearDocumentoPendenteAsync(api, processoId);
        await new ConcessoesDasPersonas(_fixture).RemoverDaAsync(PersonasDoAcesso.Leandro);

        using HttpResponseMessage response = await new RequisicoesAutenticadas(_fixture).SendAsync(
            PersonasDoAcesso.Leandro, HttpMethod.Post, $"/api/selecao/processos-seletivos/{processoId}/retificacoes", CorpoDaRetificacao(documentoPendente));

        await ConferirProcessoNaoEncontradoAsync(response);
    }

    private static async Task<Guid> SemearPublicadoAsync(CascadingApiFactory api, HttpClient client, string nome)
    {
        (Guid processoId, Guid documentoDaAbertura) = await SemearAsync(api, nome);
        using HttpResponseMessage publicacao = await PostPublicarAsync(client, processoId, documentoDaAbertura, MakeIdempotencyKey());
        publicacao.StatusCode.Should().Be(HttpStatusCode.NoContent);

        return processoId;
    }

    private static async Task<HttpResponseMessage> PostRetificacaoAsync(HttpClient client, Guid processoId, object corpo)
    {
        using HttpRequestMessage request = new(HttpMethod.Post,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/retificacoes", UriKind.Relative))
        {
            Content = JsonContent.Create(corpo),
        };
        AppendTestAuth(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
        return await client.SendAsync(request).ConfigureAwait(false);
    }
}
