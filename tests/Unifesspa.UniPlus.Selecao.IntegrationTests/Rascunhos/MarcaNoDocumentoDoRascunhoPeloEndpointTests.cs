namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static ClienteDoRascunhoDeTeste;

/// <summary>
/// A marca "no mesmo momento da anterior" no documento do rascunho, pelo HTTP: guardada na posição da fase, exportada e aceita de
/// volta sem mudar nada, e ausente no documento antigo, que continua aceito e é lido sem marca.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class MarcaNoDocumentoDoRascunhoPeloEndpointTests : IAsyncLifetime
{
    private const string Leandro = "leandro.viana";
    private const string Inscricao = """{ "chave": "inscricao", "tipoFaseCodigo": "INSCRICAO" }""";
    private const string EnsalamentoNoMesmoMomento = """{ "chave": "ensalamento", "tipoFaseCodigo": "ENSALAMENTO", "noMesmoMomentoDaAnterior": true }""";
    private const string EnsalamentoSemAMarca = """{ "chave": "ensalamento", "tipoFaseCodigo": "ENSALAMENTO" }""";

    private readonly CascadingFixture _fixture;
    private readonly ClienteDoRascunhoDeTeste _cliente;

    public MarcaNoDocumentoDoRascunhoPeloEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
        _cliente = new ClienteDoRascunhoDeTeste(fixture);
    }

    public Task InitializeAsync() => ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Leandro);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "A marca da segunda fase é guardada na posição dela, e o documento exportado é aceito de volta com a marca no mesmo lugar")]
    public async Task MarcaDoDocumento_ExportadaEReenviadaNaMesmaPosicao()
    {
        await _cliente.ConcederAsync(Leandro, UnidadesDeTeste.Ceps.OrigemId);
        Guid id = await CriarAsync($"{Inscricao}, {EnsalamentoNoMesmoMomento}");
        (await MarcasLidasAsync(id, "\"1\"")).Should().Equal(false, true);

        using HttpResponseMessage reenviado = await _cliente.EnviarAsync(
            Leandro, HttpMethod.Put, $"{Rota}/{id}", await ExportadoAsync(id), ifMatch: "\"1\"");

        reenviado.StatusCode.Should().Be(HttpStatusCode.OK, await reenviado.Content.ReadAsStringAsync());
        (await MarcasLidasAsync(id, "\"2\"")).Should().Equal(false, true);
    }

    [Fact(DisplayName = "O documento sem a marca em nenhuma fase é aceito e lido sem marca, e acrescentar a marca num reenvio a guarda")]
    public async Task DocumentoSemAMarca_AceitoELidoSemMarca()
    {
        await _cliente.ConcederAsync(Leandro, UnidadesDeTeste.Ceps.OrigemId);
        Guid id = await CriarAsync($"{Inscricao}, {EnsalamentoSemAMarca}");
        (await MarcasLidasAsync(id, "\"1\"")).Should().Equal(false, false);

        using HttpResponseMessage acrescentada = await _cliente.EnviarAsync(
            Leandro, HttpMethod.Put, $"{Rota}/{id}", DocumentoComFases($"{Inscricao}, {EnsalamentoNoMesmoMomento}"), ifMatch: "\"1\"");

        acrescentada.StatusCode.Should().Be(HttpStatusCode.OK, await acrescentada.Content.ReadAsStringAsync());
        (await MarcasLidasAsync(id, "\"2\"")).Should().Equal(false, true);
    }

    [Fact(DisplayName = "A marca que não é verdadeiro nem falso recusa o documento com 422 e nenhum rascunho é criado")]
    public async Task MarcaQueNaoEBooleana_Recusa422()
    {
        await _cliente.ConcederAsync(Leandro, UnidadesDeTeste.Ceps.OrigemId);

        using HttpResponseMessage recusado = await _cliente.EnviarAsync(
            Leandro, HttpMethod.Post, Rota,
            DocumentoComFases("""{ "chave": "inscricao", "tipoFaseCodigo": "INSCRICAO", "noMesmoMomentoDaAnterior": "sim" }"""),
            chave: Guid.CreateVersion7().ToString());

        recusado.StatusCode.Should().Be(HttpStatusCode.UnprocessableContent, await recusado.Content.ReadAsStringAsync());
    }

    private async Task<Guid> CriarAsync(string fases)
    {
        using HttpResponseMessage criado = await _cliente.EnviarAsync(
            Leandro, HttpMethod.Post, Rota, DocumentoComFases(fases), chave: Guid.CreateVersion7().ToString());

        criado.StatusCode.Should().Be(HttpStatusCode.Created, await criado.Content.ReadAsStringAsync());
        return (await Corpo(criado)).GetProperty("id").GetGuid();
    }

    private async Task<string> ExportadoAsync(Guid id)
    {
        using HttpResponseMessage lido = await _cliente.EnviarAsync(Leandro, HttpMethod.Get, $"{Rota}/{id}");

        lido.StatusCode.Should().Be(HttpStatusCode.OK);
        return await lido.Content.ReadAsStringAsync();
    }

    /// <summary>A marca de cada fase como a exportação a traz, na ordem da lista; a fase sem a propriedade conta como sem marca.</summary>
    private async Task<bool[]> MarcasLidasAsync(Guid id, string eTagEsperado)
    {
        using HttpResponseMessage lido = await _cliente.EnviarAsync(Leandro, HttpMethod.Get, $"{Rota}/{id}");

        lido.StatusCode.Should().Be(HttpStatusCode.OK);
        lido.Headers.ETag!.Tag.Should().Be(eTagEsperado);
        JsonElement fases = (await Corpo(lido)).GetProperty("cronograma").GetProperty("fases");

        return [.. fases.EnumerateArray().Select(static fase => fase.TryGetProperty("noMesmoMomentoDaAnterior", out JsonElement marca) && marca.GetBoolean())];
    }

    private static string DocumentoComFases(string fases) =>
        $$"""{ "formato": "uniplus.selecao.configuracao", "versaoFormato": 1, "identificacao": { "nome": "Processo Seletivo de Exemplo", "unidadeAdministradoraCodigo": "CEPS" }, "cronograma": { "fases": [ {{fases}} ] } }""";
}
