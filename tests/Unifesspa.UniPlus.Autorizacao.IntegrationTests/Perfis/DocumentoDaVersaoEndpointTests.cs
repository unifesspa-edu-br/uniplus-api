namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>Documento instituidor da versão pelo endpoint real: reemissão da URL, confirmação do envio, novo documento e leitura.</summary>
[Collection(AutorizacaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class DocumentoDaVersaoEndpointTests : PerfilEndpointTestBase
{
    public DocumentoDaVersaoEndpointTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A URL de envio se reemite para o mesmo documento e a confirmação o põe em verificação")]
    public async Task Documento_ReemiteEConfirma()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        using HttpResponseMessage criada = await EnviarAsync(diogo, HttpMethod.Post, Rota, Corpo(Nome(), [ConsultarCatalogo]));
        Envio envio = LerEnvio(await criada.Content.ReadAsStringAsync());
        string rota = $"{Rota}/{envio.PerfilId}/versoes/{envio.VersaoId}/documento";

        using HttpResponseMessage reemitida = await EnviarAsync(diogo, HttpMethod.Post, $"{rota}/envio");
        using HttpResponseMessage confirmada = await EnviarAsync(diogo, HttpMethod.Post, $"{rota}/confirmacao", new { etag = "\"etag-de-teste\"" });
        using HttpResponseMessage reemitidaDepois = await EnviarAsync(diogo, HttpMethod.Post, $"{rota}/envio");

        reemitida.StatusCode.Should().Be(HttpStatusCode.Created);
        LerEnvio(await reemitida.Content.ReadAsStringAsync()).DocumentoId.Should().Be(envio.DocumentoId);
        confirmada.StatusCode.Should().Be(HttpStatusCode.OK, await confirmada.Content.ReadAsStringAsync());
        (await confirmada.Content.ReadAsStringAsync()).Should().Contain("em_verificacao");
        reemitidaDepois.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await reemitidaDepois.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.documento_nao_aguarda_envio");
    }

    [Fact(DisplayName = "Confirmar sem ETag devolve 422 e o documento segue aguardando o envio")]
    public async Task Documento_ConfirmarSemEtag_Devolve422()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        using HttpResponseMessage criada = await EnviarAsync(diogo, HttpMethod.Post, Rota, Corpo(Nome(), [ConsultarCatalogo]));
        Envio envio = LerEnvio(await criada.Content.ReadAsStringAsync());

        using HttpResponseMessage resposta = await EnviarAsync(
            diogo, HttpMethod.Post, $"{Rota}/{envio.PerfilId}/versoes/{envio.VersaoId}/documento/confirmacao", new { etag = " " });

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.documento_invalido");
    }

    [Fact(DisplayName = "Outro documento sem recusa anterior e leitura de documento não verificado devolvem 422 sem URL")]
    public async Task Documento_NovoSemRecusaELeituraSemVerificacao_Recusam()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        using HttpResponseMessage criada = await EnviarAsync(diogo, HttpMethod.Post, Rota, Corpo(Nome(), [ConsultarCatalogo]));
        Envio envio = LerEnvio(await criada.Content.ReadAsStringAsync());
        string rota = $"{Rota}/{envio.PerfilId}/versoes/{envio.VersaoId}/documento";

        using HttpResponseMessage novo = await EnviarAsync(diogo, HttpMethod.Post, rota, new { tamanhoDoDocumento = 2048 });
        using HttpResponseMessage leitura = await diogo.GetAsync(new Uri(rota, UriKind.Relative));

        novo.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await novo.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.documento_nao_recusado");
        leitura.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await leitura.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.documento_nao_verificado");
    }

    [Fact(DisplayName = "Ler o documento verificado devolve a URL sem cache e grava o acesso na trilha")]
    public async Task Documento_LerVerificado_GravaATrilha()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await SemearPerfilVigenteAsync(ConsultarCatalogo);
        Guid versao = await VersaoVigenteAsync(id);

        using HttpResponseMessage resposta = await diogo.GetAsync(new Uri($"{Rota}/{id}/versoes/{versao}/documento", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        resposta.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await ContarLeiturasAsync(id)).Should().Be(1);
    }
}
