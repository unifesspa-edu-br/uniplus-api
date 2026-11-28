namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;

using AwesomeAssertions;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDasRecusasJuntasDosAtos;

/// <summary>
/// A publicação do documento avulso soma as recusas que não dependem umas das outras na mesma resposta, de ponta a
/// ponta contra o host real, e a pessoa sem permissão não vê nenhuma delas.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class RecusasJuntasDoAtoAvulsoEndpointTests
{
    private static readonly string[] CodigosDasCausasInexistentes =
    [
        "uniplus.selecao.processo_seletivo.tipo_de_ato_sem_versao_vigente",
        DocumentoNaoEncontrado,
        "uniplus.selecao.ato_do_processo.ato_corrigido_nao_encontrado",
    ];

    private readonly CascadingFixture _fixture;

    public RecusasJuntasDoAtoAvulsoEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "O tipo, o documento principal e o ato corrigido inexistentes saem na mesma resposta 422, na ordem do pedido, e nada é gravado")]
    public async Task TipoDocumentoEAtoCorrigidoInexistentes_SaemJuntosENadaEGravado()
    {
        (CascadingApiFactory api, HttpClient client, Guid processoId) = await PrepararAsync();
        Dictionary<string, object?> campos = CamposDoAvulso(Guid.CreateVersion7());
        campos["tipoAtoCodigo"] = "TIPO_INEXISTENTE";
        campos["atoCorrigidoId"] = Guid.CreateVersion7();
        campos["motivoDaCorrecao"] = "Data errada";

        using HttpResponseMessage resposta = await EnviarAsync(client, Rota(processoId), campos);

        await ExigirCausasAsync(resposta, CodigosDasCausasInexistentes, "tipoAtoCodigo", "documentoPrincipalId", "atoCorrigidoId");
        (await ContarAtosDoProcessoAsync(api, processoId)).Should().Be(1, "só o edital de abertura está registrado");
    }

    [Theory(DisplayName = "O assinante ausente, nulo, vazio ou em branco é recusado com 422 no campo dele, nunca com 400")]
    [InlineData("ausente")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AssinanteAusente_Recusa422NoCampoDele(string? assinante)
    {
        (CascadingApiFactory api, HttpClient client, Guid processoId) = await PrepararAsync();
        Dictionary<string, object?> campos = CamposDoAvulso(await DocumentoAsync(api, processoId));
        ColocarOuRetirar(campos, "assinante", assinante);

        using HttpResponseMessage resposta = await EnviarAsync(client, Rota(processoId), campos);

        await ExigirCausasAsync(resposta, [Obrigatorio], "assinante");
    }

    [Theory(DisplayName = "O tipo ausente, nulo, vazio ou em branco é recusado com 422 no campo dele, nunca com 400")]
    [InlineData("ausente")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TipoAusente_Recusa422NoCampoDele(string? tipo)
    {
        (CascadingApiFactory api, HttpClient client, Guid processoId) = await PrepararAsync();
        Dictionary<string, object?> campos = CamposDoAvulso(await DocumentoAsync(api, processoId));
        ColocarOuRetirar(campos, "tipoAtoCodigo", tipo);

        using HttpResponseMessage resposta = await EnviarAsync(client, Rota(processoId), campos);

        await ExigirCausasAsync(resposta, [Obrigatorio], "tipoAtoCodigo");
    }

    [Fact(DisplayName = "O assinante ausente sai junto da recusa do documento principal, na ordem do pedido")]
    public async Task AssinanteAusenteEDocumentoInexistente_SaemJuntos()
    {
        (_, HttpClient client, Guid processoId) = await PrepararAsync();
        Dictionary<string, object?> campos = CamposDoAvulso(Guid.CreateVersion7());
        campos.Remove("assinante");

        using HttpResponseMessage resposta = await EnviarAsync(client, Rota(processoId), campos);

        await ExigirCausasAsync(resposta, [Obrigatorio, DocumentoNaoEncontrado], "assinante", "documentoPrincipalId");
    }

    [Fact(DisplayName = "A pessoa sem permissão recebe 404 antes de qualquer causa, mesmo com todas as recusas no pedido")]
    public async Task SemPermissao_RecebeNaoEncontradoAntesDeQualquerRecusa()
    {
        (_, HttpClient client, Guid processoId) = await PrepararAsync();
        Dictionary<string, object?> campos = CamposDoAvulso(Guid.CreateVersion7());
        campos["tipoAtoCodigo"] = "TIPO_INEXISTENTE";
        campos["atoCorrigidoId"] = Guid.CreateVersion7();
        campos.Remove("assinante");

        using HttpResponseMessage resposta = await EnviarSemPermissaoAsync(client, Rota(processoId), campos);

        await PublicarAtoAvulsoEndpointTests.ExigirProblemaAsync(
            resposta, "uniplus.selecao.processo_seletivo.nao_encontrado", HttpStatusCode.NotFound);
        (await resposta.Content.ReadAsStringAsync()).Should().NotContain("tipoAtoCodigo").And.NotContain("assinante");
    }

    private static string Rota(Guid processoId) => $"/api/selecao/processos-seletivos/{processoId}/atos";

    private static void ColocarOuRetirar(Dictionary<string, object?> campos, string campo, string? valor)
    {
        if (valor == "ausente")
        {
            campos.Remove(campo);
            return;
        }

        campos[campo] = valor;
    }

    private async Task<(CascadingApiFactory Api, HttpClient Client, Guid ProcessoId)> PrepararAsync()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        HttpClient client = api.CreateClient();
        (Guid processoId, _) = await PublicarAtoAvulsoEndpointTests.PublicarEditalAsync(api, client);
        return (api, client, processoId);
    }

    private static Task<Guid> DocumentoAsync(CascadingApiFactory api, Guid processoId) =>
        PublicarAtoAvulsoEndpointTests.SemearDocumentoAsync(api, processoId, Domain.Services.TiposDeMidiaDeDocumento.Pdf, "aviso.pdf");
}
