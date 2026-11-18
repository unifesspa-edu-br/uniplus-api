namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static ClienteDoRascunhoDeTeste;

/// <summary>
/// As chaves das partes do documento: quem cria a parte escolhe a chave e a API a guarda como veio; a parte sem chave recusa o
/// salvamento, e reordenar as partes com as suas chaves não troca a chave de nenhuma.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class ChavesDoRascunhoEndpointTests : IAsyncLifetime
{
    private const string Leandro = "leandro.viana";
    private const string Prova = """{ "chave": "prova-objetiva", "nome": "Prova objetiva" }""";
    private const string Redacao = """{ "chave": "redacao", "nome": "Redação" }""";

    private readonly CascadingFixture _fixture;
    private readonly ClienteDoRascunhoDeTeste _cliente;

    public ChavesDoRascunhoEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
        _cliente = new ClienteDoRascunhoDeTeste(fixture);
    }

    public Task InitializeAsync() => ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Leandro);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "A chave que veio na parte volta igual na releitura e, com as partes reordenadas, cada chave segue a sua parte")]
    public async Task ChaveEscolhida_VoltaIgualEAcompanhaAParteAoReordenar()
    {
        await _cliente.ConcederAsync(Leandro, UnidadesDeTeste.Ceps.OrigemId);

        (Guid id, string eTagDoSalvar) = await CriarAsync($"{Prova}, {Redacao}");
        (await ChavesDasEtapasNaReleituraAsync(id, eTagDoSalvar)).Should().Equal(("prova-objetiva", "Prova objetiva"), ("redacao", "Redação"));

        await SubstituirAsync(id, eTagDoSalvar, $"{Redacao}, {Prova}");

        (await ChavesDasEtapasNaReleituraAsync(id, "\"2\"")).Should().Equal(("redacao", "Redação"), ("prova-objetiva", "Prova objetiva"));
    }

    [Fact(DisplayName = "A parte sem chave recusa a criação com 422, no ponteiro da chave que falta, e nenhum rascunho é criado")]
    public async Task ParteSemChave_Cria422NoPonteiroDaChave()
    {
        await _cliente.ConcederAsync(Leandro, UnidadesDeTeste.Ceps.OrigemId);
        int rascunhosAntes = await ContarRascunhosAsync();

        using HttpResponseMessage resposta = await _cliente.EnviarAsync(
            Leandro, HttpMethod.Post, Rota, DocumentoComEtapas($$"""{ "nome": "Prova objetiva" }"""), chave: Guid.CreateVersion7().ToString());

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableContent);
        (await CausasAsync(resposta)).Should().Contain(
            ("/etapas/0/chave", "uniplus.selecao.documento_da_configuracao.campo_obrigatorio_ausente"));
        (await ContarRascunhosAsync()).Should().Be(rascunhosAntes, "a criação recusada não grava rascunho");
    }

    [Fact(DisplayName = "O PUT que devolve uma parte sem chave recusa com 422 e conserva a revisão anterior")]
    public async Task PutComParteSemChave_Recusa422EConservaARevisao()
    {
        await _cliente.ConcederAsync(Leandro, UnidadesDeTeste.Ceps.OrigemId);
        (Guid id, string eTagDoSalvar) = await CriarAsync($"{Prova}, {Redacao}");
        string documentoAntes = await DocumentoNaReleituraAsync(id, eTagDoSalvar);

        using HttpResponseMessage resposta = await _cliente.EnviarAsync(
            Leandro, HttpMethod.Put, $"{Rota}/{id}", DocumentoComEtapas($$"""{ "nome": "Redação" }, {{Prova}}"""), ifMatch: eTagDoSalvar);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableContent);
        (await CausasAsync(resposta)).Should().Contain(
            ("/etapas/0/chave", "uniplus.selecao.documento_da_configuracao.campo_obrigatorio_ausente"));
        (await DocumentoNaReleituraAsync(id, eTagDoSalvar)).Should().Be(documentoAntes, "a substituição recusada conserva o documento e a revisão");
    }

    private async Task<(Guid Id, string ETag)> CriarAsync(string etapas)
    {
        using HttpResponseMessage criado = await _cliente.EnviarAsync(
            Leandro, HttpMethod.Post, Rota, DocumentoComEtapas(etapas), chave: Guid.CreateVersion7().ToString());

        criado.StatusCode.Should().Be(HttpStatusCode.Created);
        return ((await Corpo(criado)).GetProperty("id").GetGuid(), criado.Headers.ETag!.Tag);
    }

    private async Task SubstituirAsync(Guid id, string ifMatch, string etapas)
    {
        using HttpResponseMessage substituido = await _cliente.EnviarAsync(
            Leandro, HttpMethod.Put, $"{Rota}/{id}", DocumentoComEtapas(etapas), ifMatch: ifMatch);

        substituido.StatusCode.Should().Be(HttpStatusCode.OK);
        substituido.Headers.ETag!.Tag.Should().Be("\"2\"");
    }

    private async Task<(string Chave, string Nome)[]> ChavesDasEtapasNaReleituraAsync(Guid id, string eTagEsperado)
    {
        using HttpResponseMessage lido = await _cliente.EnviarAsync(Leandro, HttpMethod.Get, $"{Rota}/{id}");

        lido.StatusCode.Should().Be(HttpStatusCode.OK);
        lido.Headers.ETag!.Tag.Should().Be(eTagEsperado);
        JsonElement exportado = await Corpo(lido);
        return [.. exportado.GetProperty("etapas").EnumerateArray().Select(static etapa => (etapa.GetProperty("chave").GetString()!, etapa.GetProperty("nome").GetString()!))];
    }

    private async Task<string> DocumentoNaReleituraAsync(Guid id, string eTagEsperado)
    {
        using HttpResponseMessage lido = await _cliente.EnviarAsync(Leandro, HttpMethod.Get, $"{Rota}/{id}");

        lido.StatusCode.Should().Be(HttpStatusCode.OK);
        lido.Headers.ETag!.Tag.Should().Be(eTagEsperado);
        return await lido.Content.ReadAsStringAsync();
    }

    private async Task<int> ContarRascunhosAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext banco = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return await banco.RascunhosDeProcessosSeletivos.IgnoreQueryFilters().CountAsync();
    }

    private static async Task<(string? Campo, string Codigo)[]> CausasAsync(HttpResponseMessage resposta) =>
        [.. (await Corpo(resposta)).GetProperty("errors").EnumerateArray()
            .Select(static erro => (erro.TryGetProperty("field", out JsonElement campo) ? campo.GetString() : null, erro.GetProperty("code").GetString()!))];

    private static string DocumentoComEtapas(string etapas) =>
        $$"""{ "formato": "uniplus.selecao.configuracao", "versaoFormato": 1, "identificacao": { "nome": "Processo Seletivo de Exemplo", "unidadeAdministradoraCodigo": "CEPS" }, "etapas": [ {{etapas}} ] }""";
}
