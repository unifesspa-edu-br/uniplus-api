namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;

using AwesomeAssertions;

using Domain.Services;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDasRecusasJuntasDosAtos;

/// <summary>
/// A publicação de um item do edital soma as recusas que não dependem umas das outras na mesma resposta, de ponta a
/// ponta contra o host real, e a pessoa sem permissão não vê nenhuma delas.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class RecusasJuntasDoProdutoDoEditalEndpointTests
{
    private readonly CascadingFixture _fixture;

    public RecusasJuntasDoProdutoDoEditalEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "O item não declarado e o documento inexistente saem na mesma resposta 422, o do documento com o campo dele")]
    public async Task ItemNaoDeclaradoEDocumentoInexistente_SaemJuntos()
    {
        (CascadingApiFactory api, HttpClient client, Guid processoId, _) = await PrepararAsync();

        using HttpResponseMessage resposta = await EnviarAsync(
            client, Rota(processoId, Guid.CreateVersion7()), CamposDoItem(Guid.CreateVersion7()));

        await ExigirCausasAsync(resposta, ["uniplus.selecao.ato_do_processo.produto_nao_declarado", DocumentoNaoEncontrado], null, "documentoPrincipalId");
        (await ContarAtosDoProcessoAsync(api, processoId)).Should().Be(1, "só o edital de abertura está registrado");
    }

    [Fact(DisplayName = "O item já publicado e o documento inexistente saem na mesma resposta 422")]
    public async Task ItemJaPublicadoEDocumentoInexistente_SaemJuntos()
    {
        (CascadingApiFactory api, HttpClient client, Guid processoId, Guid preliminar) = await PrepararAsync();
        using (HttpResponseMessage primeira = await EnviarAsync(client, Rota(processoId, preliminar), CamposDoItem(await DocumentoAsync(api, processoId))))
        {
            primeira.StatusCode.Should().Be(HttpStatusCode.Created, await primeira.Content.ReadAsStringAsync());
        }

        using HttpResponseMessage resposta = await EnviarAsync(client, Rota(processoId, preliminar), CamposDoItem(Guid.CreateVersion7()));

        await ExigirCausasAsync(resposta, ["uniplus.selecao.ato_do_processo.produto_ja_publicado", DocumentoNaoEncontrado], null, "documentoPrincipalId");
    }

    [Theory(DisplayName = "O assinante ausente, nulo, vazio ou em branco é recusado com 422 no campo dele, nunca com 400")]
    [InlineData("ausente")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AssinanteAusente_Recusa422NoCampoDele(string? assinante)
    {
        (CascadingApiFactory api, HttpClient client, Guid processoId, Guid preliminar) = await PrepararAsync();
        Dictionary<string, object?> campos = CamposDoItem(await DocumentoAsync(api, processoId));
        if (assinante == "ausente")
        {
            campos.Remove("assinante");
        }
        else
        {
            campos["assinante"] = assinante;
        }

        using HttpResponseMessage resposta = await EnviarAsync(client, Rota(processoId, preliminar), campos);

        await ExigirCausasAsync(resposta, [Obrigatorio], "assinante");
    }

    [Fact(DisplayName = "O assinante ausente sai junto do item não declarado, forma primeiro")]
    public async Task AssinanteAusenteEItemNaoDeclarado_SaemJuntos()
    {
        (CascadingApiFactory api, HttpClient client, Guid processoId, _) = await PrepararAsync();
        Dictionary<string, object?> campos = CamposDoItem(await DocumentoAsync(api, processoId));
        campos.Remove("assinante");

        using HttpResponseMessage resposta = await EnviarAsync(client, Rota(processoId, Guid.CreateVersion7()), campos);

        await ExigirCausasAsync(resposta, [Obrigatorio, "uniplus.selecao.ato_do_processo.produto_nao_declarado"], "assinante", null);
    }

    [Fact(DisplayName = "A pessoa sem permissão recebe 404 antes de qualquer causa, mesmo com todas as recusas no pedido")]
    public async Task SemPermissao_RecebeNaoEncontradoAntesDeQualquerRecusa()
    {
        (_, HttpClient client, Guid processoId, _) = await PrepararAsync();
        Dictionary<string, object?> campos = CamposDoItem(Guid.CreateVersion7());
        campos.Remove("assinante");

        using HttpResponseMessage resposta = await EnviarSemPermissaoAsync(client, Rota(processoId, Guid.CreateVersion7()), campos);

        await PublicarAtoAvulsoEndpointTests.ExigirProblemaAsync(
            resposta, "uniplus.selecao.processo_seletivo.nao_encontrado", HttpStatusCode.NotFound);
        (await resposta.Content.ReadAsStringAsync()).Should().NotContain("assinante").And.NotContain("documentoPrincipalId");
    }

    private static string Rota(Guid processoId, Guid produtoId) =>
        $"/api/selecao/processos-seletivos/{processoId}/produtos-previstos/{produtoId}/publicacoes";

    private async Task<(CascadingApiFactory Api, HttpClient Client, Guid ProcessoId, Guid Preliminar)> PrepararAsync()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        HttpClient client = api.CreateClient();
        (Guid processoId, _) = await PublicarAtoAvulsoEndpointTests.PublicarEditalAsync(
            api, client, PublicarProdutoDoEditalEndpointTests.ProdutosDaHomologacao());
        System.Text.Json.JsonElement itens = await PublicarProdutoDoEditalEndpointTests.ConsultarItensAsync(client, processoId);
        Guid preliminar = itens.GetProperty("fases").EnumerateArray()
            .SelectMany(static f => f.GetProperty("produtos").EnumerateArray())
            .Single(static p => p.GetProperty("papel").GetProperty("codigo").GetString() == "PRELIMINAR").GetProperty("id").GetGuid();
        return (api, client, processoId, preliminar);
    }

    private static Task<Guid> DocumentoAsync(CascadingApiFactory api, Guid processoId) =>
        PublicarAtoAvulsoEndpointTests.SemearDocumentoAsync(api, processoId, TiposDeMidiaDeDocumento.Pdf, "lista.pdf");
}
