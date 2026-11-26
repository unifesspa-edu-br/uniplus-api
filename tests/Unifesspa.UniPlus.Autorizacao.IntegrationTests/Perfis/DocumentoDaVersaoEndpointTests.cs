namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis;

using System.Net;
using System.Net.Http.Headers;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>Documento instituidor da versão pelo endpoint real: reemissão da URL, confirmação do envio, novo documento e leitura.</summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class DocumentoDaVersaoEndpointTests : PerfilEndpointTestBase
{
    private const string Tipo = "application/vnd.uniplus.documento-da-versao.v1+json";

    public DocumentoDaVersaoEndpointTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A URL de envio se reemite para o mesmo documento e a confirmação o põe em verificação")]
    public async Task Documento_ReemiteEConfirma()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        using CenarioDeConfirmacao cenario = new(diogo);
        await cenario.ExecutarAsync();
        cenario.Reemitida.StatusCode.Should().Be(HttpStatusCode.Created);
        LerEnvio(await cenario.Reemitida.Content.ReadAsStringAsync()).DocumentoId.Should().Be(cenario.Envio.DocumentoId);
        cenario.Confirmada.StatusCode.Should().Be(HttpStatusCode.OK, await cenario.Confirmada.Content.ReadAsStringAsync());
        (await cenario.Confirmada.Content.ReadAsStringAsync()).Should().Contain("EM_VERIFICACAO");
        cenario.ReemitidaDepois.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await cenario.ReemitidaDepois.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.documento_nao_aguarda_envio");
    }

    private sealed class CenarioDeConfirmacao(HttpClient cliente) : IDisposable
    {
        public Envio Envio { get; private set; } = null!;
        public HttpResponseMessage Reemitida { get; private set; } = null!;
        public HttpResponseMessage Confirmada { get; private set; } = null!;
        public HttpResponseMessage ReemitidaDepois { get; private set; } = null!;

        public async Task ExecutarAsync()
        {
            using HttpResponseMessage criada = await EnviarAsync(cliente, HttpMethod.Post, Rota, Corpo(Nome(), [ConsultarCatalogo]));
            Envio = LerEnvio(await criada.Content.ReadAsStringAsync());
            string rota = $"{Rota}/{Envio.PerfilId}/versoes/{Envio.VersaoId}/documento";
            Reemitida = await EnviarAsync(cliente, HttpMethod.Post, $"{rota}/envio");
            Confirmada = await EnviarAsync(cliente, HttpMethod.Post, $"{rota}/confirmacao", new { etag = "\"etag-de-teste\"" });
            ReemitidaDepois = await EnviarAsync(cliente, HttpMethod.Post, $"{rota}/envio");
        }

        public void Dispose()
        {
            Reemitida?.Dispose();
            Confirmada?.Dispose();
            ReemitidaDepois?.Dispose();
        }
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
        using HttpResponseMessage leitura = await LerDocumentoAsync(diogo, rota, Tipo);

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

        using HttpResponseMessage resposta = await LerDocumentoAsync(diogo, $"{Rota}/{id}/versoes/{versao}/documento", Tipo);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        resposta.Content.Headers.ContentType!.MediaType.Should().Be(Tipo);
        resposta.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await ContarLeiturasAsync(id)).Should().Be(1);
    }

    [Fact(DisplayName = "O documento devolve a versão atual para application/json")]
    public async Task Documento_JsonSemVersao_DevolveTipoDaVersaoAtual()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await SemearPerfilVigenteAsync(ConsultarCatalogo);
        Guid versao = await VersaoVigenteAsync(id);
        using HttpResponseMessage resposta = await LerDocumentoAsync(diogo, $"{Rota}/{id}/versoes/{versao}/documento", "application/json");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        resposta.Content.Headers.ContentType!.MediaType.Should().Be(Tipo);
    }

    [Fact(DisplayName = "Versão de mídia inexistente recusa a leitura sem URL nem acesso")]
    public async Task Documento_VersaoDeMidiaInexistente_Devolve406SemLeitura()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await SemearPerfilVigenteAsync(ConsultarCatalogo);
        Guid versao = await VersaoVigenteAsync(id);
        using HttpResponseMessage resposta = await LerDocumentoAsync(
            diogo, $"{Rota}/{id}/versoes/{versao}/documento", "application/vnd.uniplus.documento-da-versao.v99+json");

        await VerificarRecusaDeVersaoAsync(resposta);
        (await resposta.Content.ReadAsStringAsync()).Should().NotContain(id.ToString()).And.NotContain(versao.ToString());
        (await ContarLeiturasAsync(id)).Should().Be(0);
    }

    [Theory(DisplayName = "Sem autenticação ou permissão, o documento não é lido")]
    [InlineData(false, HttpStatusCode.Unauthorized)]
    [InlineData(true, HttpStatusCode.Forbidden)]
    public async Task Documento_SemAcesso_RecusaAntesDaLeitura(bool autenticado, HttpStatusCode esperado)
    {
        Guid id = await SemearPerfilVigenteAsync(ConsultarCatalogo);
        Guid versao = await VersaoVigenteAsync(id);
        using HttpClient cliente = autenticado ? Cliente("pessoa.sem.permissao") : Fixture.Factory.CreateClient();
        using HttpResponseMessage resposta = await LerDocumentoAsync(cliente, $"{Rota}/{id}/versoes/{versao}/documento", Tipo);

        resposta.StatusCode.Should().Be(esperado);
        (await ContarLeiturasAsync(id)).Should().Be(0);
    }

    private static async Task<HttpResponseMessage> LerDocumentoAsync(HttpClient cliente, string rota, string tipo)
    {
        using HttpRequestMessage pedido = new(HttpMethod.Get, new Uri(rota, UriKind.Relative));
        pedido.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(tipo));
        return await cliente.SendAsync(pedido);
    }
}
