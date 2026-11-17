namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static ClienteDoRascunhoDeTeste;

/// <summary>
/// O ciclo HTTP do rascunho do processo seletivo e a recusa por unidade: criar, exportar, substituir sob a revisão lida,
/// recusar a revisão velha e excluir; e quem não configura na unidade do rascunho o encontra como inexistente.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class RascunhoDeProcessoSeletivoEndpointTests : IAsyncLifetime
{
    private const string Leandro = "leandro.viana";
    private const string Diogo = "diogo.souza";

    private readonly CascadingFixture _fixture;
    private readonly ClienteDoRascunhoDeTeste _cliente;

    public RascunhoDeProcessoSeletivoEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
        _cliente = new ClienteDoRascunhoDeTeste(fixture);
    }

    public async Task InitializeAsync()
    {
        foreach (string contaId in new[] { Leandro, Diogo })
        {
            await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, contaId);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "O ciclo cria, exporta, substitui, recusa a revisão velha e exclui o rascunho")]
    public async Task Ciclo_CriaExportaSubstituiRecusaRevisaoVelhaEExclui()
    {
        await _cliente.ConcederAsync(Leandro, UnidadesDeTeste.Ceps.OrigemId);

        Guid id = await _cliente.CriarAsync(Leandro, "CEPS");
        await ExportadoComOIdEOsDescritivosAsync(id);
        await SubstituidoNaRevisao1Async(id);

        using HttpResponseMessage obsoleta = await _cliente.EnviarAsync(Leandro, HttpMethod.Put, $"{Rota}/{id}", Documento("CEPS", "Terceiro nome"), ifMatch: "\"1\"");
        using HttpResponseMessage excluido = await _cliente.EnviarAsync(Leandro, HttpMethod.Delete, $"{Rota}/{id}", ifMatch: "\"2\"");
        using HttpResponseMessage depois = await _cliente.EnviarAsync(Leandro, HttpMethod.Get, $"{Rota}/{id}");

        obsoleta.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        excluido.StatusCode.Should().Be(HttpStatusCode.NoContent);
        depois.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "Quem não configura na unidade do rascunho o encontra como inexistente, sem ler o corpo nem o If-Match")]
    public async Task UnidadeSemPermissao_RecusaComOMesmo404DoRascunhoInexistente()
    {
        await _cliente.ConcederAsync(Leandro, UnidadesDeTeste.Ceps.OrigemId);
        await _cliente.ConcederAsync(Diogo, UnidadesDeTeste.Cga.OrigemId);
        Guid id = await _cliente.CriarAsync(Leandro, "CEPS");

        using HttpResponseMessage lido = await _cliente.EnviarAsync(Diogo, HttpMethod.Get, $"{Rota}/{id}");
        using HttpResponseMessage substituido = await _cliente.EnviarAsync(Diogo, HttpMethod.Put, $"{Rota}/{id}", "{ não é json", ifMatch: "\"1\"");
        using HttpResponseMessage excluido = await _cliente.EnviarAsync(Diogo, HttpMethod.Delete, $"{Rota}/{id}");
        using HttpResponseMessage inexistente = await _cliente.EnviarAsync(Diogo, HttpMethod.Get, $"{Rota}/{Guid.CreateVersion7()}");

        new[] { lido, substituido, excluido, inexistente }.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "Criar o rascunho numa unidade em que a pessoa não configura recusa 403, como o código de unidade que não existe")]
    public async Task CriarNaUnidadeSemPermissao_403()
    {
        await _cliente.ConcederAsync(Diogo, UnidadesDeTeste.Cga.OrigemId);

        using HttpResponseMessage semPermissao = await _cliente.EnviarAsync(Diogo, HttpMethod.Post, Rota, Documento("CEPS"), chave: Guid.CreateVersion7().ToString());
        using HttpResponseMessage inexistente = await _cliente.EnviarAsync(Diogo, HttpMethod.Post, Rota, Documento("NAOEXISTE"), chave: Guid.CreateVersion7().ToString());

        semPermissao.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        inexistente.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Corpo(semPermissao)).GetProperty("detail").GetString()
            .Should().Be((await Corpo(inexistente)).GetProperty("detail").GetString());
    }

    [Fact(DisplayName = "Substituir e excluir sem If-Match recusam 428 e o If-Match malformado recusa 400")]
    public async Task SemIfMatch_428EMalformado_400()
    {
        await _cliente.ConcederAsync(Leandro, UnidadesDeTeste.Ceps.OrigemId);
        Guid id = await _cliente.CriarAsync(Leandro, "CEPS");

        using HttpResponseMessage substituirSemIfMatch = await _cliente.EnviarAsync(Leandro, HttpMethod.Put, $"{Rota}/{id}", Documento("CEPS"));
        using HttpResponseMessage excluirSemIfMatch = await _cliente.EnviarAsync(Leandro, HttpMethod.Delete, $"{Rota}/{id}");
        using HttpResponseMessage malformado = await _cliente.EnviarAsync(Leandro, HttpMethod.Delete, $"{Rota}/{id}", ifMatch: "sem-aspas");

        substituirSemIfMatch.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
        excluirSemIfMatch.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
        malformado.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task ExportadoComOIdEOsDescritivosAsync(Guid id)
    {
        using HttpResponseMessage lido = await _cliente.EnviarAsync(Leandro, HttpMethod.Get, $"{Rota}/{id}");

        lido.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement exportado = await Corpo(lido);
        exportado.GetProperty("id").GetGuid().Should().Be(id);
        exportado.GetProperty("identificacao").GetProperty("unidadeAdministradoraSigla").GetString().Should().Be("CEPS");
    }

    private async Task SubstituidoNaRevisao1Async(Guid id)
    {
        using HttpResponseMessage substituido = await _cliente.EnviarAsync(Leandro, HttpMethod.Put, $"{Rota}/{id}", Documento("CEPS", "Segundo nome"), ifMatch: "\"1\"");

        substituido.StatusCode.Should().Be(HttpStatusCode.OK);
        substituido.Headers.ETag!.Tag.Should().Be("\"2\"");
        (await Corpo(substituido)).GetProperty("revisao").GetInt32().Should().Be(2);
    }
}
