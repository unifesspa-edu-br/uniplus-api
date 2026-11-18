namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static ClienteDoRascunhoDeTeste;

/// <summary>
/// Duas operações simultâneas sobre o mesmo rascunho, ambas sob a revisão 1: o bloqueio da linha as serializa, a primeira vence e
/// a segunda encontra o rascunho já mudado. Cada cenário se repete em repetições, cada uma num rascunho novo, porque a ordem em
/// que as duas chegam ao bloqueio varia.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class ConcorrenciaDoRascunhoDeProcessoSeletivoTests : IAsyncLifetime
{
    private const string Leandro = "leandro.viana";
    private const int Repeticoes = 5;

    private readonly CascadingFixture _fixture;
    private readonly ClienteDoRascunhoDeTeste _cliente;

    public ConcorrenciaDoRascunhoDeProcessoSeletivoTests(CascadingFixture fixture)
    {
        _fixture = fixture;
        _cliente = new ClienteDoRascunhoDeTeste(fixture);
    }

    public async Task InitializeAsync()
    {
        await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Leandro);
        await _cliente.ConcederAsync(Leandro, UnidadesDeTeste.Ceps.OrigemId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "Dois PUT simultâneos sob o mesmo If-Match terminam em um 200 e um 412, e a revisão sobe uma só vez")]
    public async Task DoisPutsComOMesmoIfMatch_UmVenceEOOutroFalhaNaPrecondicao()
    {
        for (int repeticao = 0; repeticao < Repeticoes; repeticao++)
        {
            Guid id = await _cliente.CriarAsync(Leandro, "CEPS");

            HttpStatusCode[] status = await EnviarJuntosAsync(Substituir(id, "Primeiro nome"), Substituir(id, "Segundo nome"));

            status.Order().Should().Equal(HttpStatusCode.OK, HttpStatusCode.PreconditionFailed);
            await RascunhoDeveEstarNaRevisao2ComONomeAsync(id, NomeDoVencedor(status));
        }
    }

    // A serialização sob o bloqueio é provada nos testes de bloqueio do repositório; aqui se confere o resultado observável.
    // Cada lista traz o status do PUT e o do DELETE, nessa ordem.
    private static readonly HttpStatusCode[] QuandoOPutVence = [HttpStatusCode.OK, HttpStatusCode.PreconditionFailed];
    private static readonly HttpStatusCode[] QuandoODeleteVence = [HttpStatusCode.NotFound, HttpStatusCode.NoContent];

    [Fact(DisplayName = "PUT e DELETE simultâneos sob o mesmo If-Match: se o PUT vence, o DELETE recebe 412; se o DELETE vence, o PUT recebe 404")]
    public async Task PutEDeleteComOMesmoIfMatch_UmSoTemExito()
    {
        for (int repeticao = 0; repeticao < Repeticoes; repeticao++)
        {
            Guid id = await _cliente.CriarAsync(Leandro, "CEPS");

            HttpStatusCode[] status = await EnviarJuntosAsync(Substituir(id, "Nome do PUT"), Excluir(id));

            bool substituiuVenceu = status[0] == HttpStatusCode.OK;
            status.Should().Equal(substituiuVenceu ? QuandoOPutVence : QuandoODeleteVence);
            await EstadoFinalDeveSerODoVencedorAsync(id, substituiuVenceu);
        }
    }

    private static string NomeDoVencedor(HttpStatusCode[] status) => status[0] == HttpStatusCode.OK ? "Primeiro nome" : "Segundo nome";

    private static HttpRequestMessage Substituir(Guid id, string nome) =>
        Requisicao(Leandro, HttpMethod.Put, $"{Rota}/{id}", Documento("CEPS", nome), ifMatch: "\"1\"");

    private static HttpRequestMessage Excluir(Guid id) =>
        Requisicao(Leandro, HttpMethod.Delete, $"{Rota}/{id}", ifMatch: "\"1\"");

    private async Task<HttpStatusCode[]> EnviarJuntosAsync(params HttpRequestMessage[] requisicoes)
    {
        InicioSimultaneoDeTeste inicio = new(requisicoes.Length);

        return await Task.WhenAll(requisicoes.Select(requisicao => EnviarAoMesmoTempoAsync(requisicao, inicio)));
    }

    private async Task<HttpStatusCode> EnviarAoMesmoTempoAsync(HttpRequestMessage requisicao, InicioSimultaneoDeTeste inicio)
    {
        using (requisicao)
        {
            using HttpClient cliente = _fixture.Factory.CreateClient();
            await inicio.AguardarOsOutrosAsync();
            using HttpResponseMessage resposta = await cliente.SendAsync(requisicao);
            return resposta.StatusCode;
        }
    }

    private async Task EstadoFinalDeveSerODoVencedorAsync(Guid id, bool substituiuVenceu)
    {
        if (substituiuVenceu)
        {
            await RascunhoDeveEstarNaRevisao2ComONomeAsync(id, "Nome do PUT");
            return;
        }

        using HttpResponseMessage lido = await _cliente.EnviarAsync(Leandro, HttpMethod.Get, $"{Rota}/{id}");
        lido.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task RascunhoDeveEstarNaRevisao2ComONomeAsync(Guid id, string nome)
    {
        using HttpResponseMessage lido = await _cliente.EnviarAsync(Leandro, HttpMethod.Get, $"{Rota}/{id}");

        lido.StatusCode.Should().Be(HttpStatusCode.OK);
        lido.Headers.ETag!.Tag.Should().Be("\"2\"");
        JsonElement exportado = await Corpo(lido);
        exportado.GetProperty("identificacao").GetProperty("nome").GetString().Should().Be(nome);
    }
}
