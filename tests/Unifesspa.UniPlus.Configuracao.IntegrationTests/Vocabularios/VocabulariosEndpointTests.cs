namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Vocabularios;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// Contrato HTTP dos dois vocabulários fechados de Configuração (UNI-REQ-0139). O que se
/// prova aqui é o que o fitness test não alcança: que as rotas existem, respondem sem
/// autenticação, negociam a vendor MIME e não têm par de escrita.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class VocabulariosEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public VocabulariosEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /vocabularios/tipos-banca devolve os seis códigos com nome, na ordem publicada, sem exigir autenticação")]
    public async Task ListarCodigosTipoBanca_DevolveVocabularioCompleto()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/configuracao/vocabularios/tipos-banca", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/vnd.uniplus.codigo-tipo-banca.v1+json");

        TipoBancaVocabularioDto[] codigos =
            (await response.Content.ReadFromJsonAsync<TipoBancaVocabularioDto[]>())!;

        // Afirmado por extenso, e não derivado do catálogo: é o contrato publicado que está
        // sob teste — derivá-lo da mesma fonte que o produz faria o teste concordar com
        // qualquer mudança, inclusive uma remoção acidental de código.
        codigos.Select(c => c.Codigo).Should().Equal(
            "BANCA_ANALISE_DOCUMENTAL",
            "BANCA_ENTREVISTA",
            "BANCA_CORRECAO_REDACOES",
            "BANCA_ANALISE_RECURSOS",
            "BANCA_HETEROIDENTIFICACAO",
            "BANCA_BIOPSICOSSOCIAL");
        codigos.Should().AllSatisfy(c => c.Nome.Should().NotBeNullOrWhiteSpace());
    }

    [Fact(DisplayName = "GET /vocabularios/fases-canonicas devolve os dezesseis códigos com nome, na ordem publicada, sem exigir autenticação")]
    public async Task ListarCodigosFaseCanonica_DevolveVocabularioCompleto()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/configuracao/vocabularios/fases-canonicas", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/vnd.uniplus.codigo-fase-canonica.v1+json");

        FaseCanonicaVocabularioDto[] codigos =
            (await response.Content.ReadFromJsonAsync<FaseCanonicaVocabularioDto[]>())!;

        codigos.Select(c => c.Codigo).Should().Equal(
            "INSCRICAO",
            "SOLICITACAO_ISENCAO",
            "HOMOLOGACAO",
            "ENSALAMENTO",
            "AVALIACAO",
            "CLASSIFICACAO",
            "RESULTADO_PRELIMINAR",
            "RECURSOS",
            "RESULTADO_FINAL",
            "HABILITACAO",
            "HETEROIDENTIFICACAO",
            "AVALIACAO_BIOPSICOSSOCIAL",
            "MATRICULA",
            "HOMOLOGACAO_RESULTADO_FINAL",
            "LISTA_ESPERA",
            "CHAMADA");
        codigos.Should().AllSatisfy(c => c.Nome.Should().NotBeNullOrWhiteSpace());
    }

    [Theory(DisplayName = "Vocabulário governado por código não tem rota de escrita — evolução é mudança versionada da API, não cadastro")]
    [InlineData("/api/configuracao/vocabularios/tipos-banca")]
    [InlineData("/api/configuracao/vocabularios/fases-canonicas")]
    public async Task Vocabularios_NaoTemRotaDeEscrita(string rota)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Uri uri = new(rota, UriKind.Relative);

        HttpResponseMessage post = await client.PostAsJsonAsync(uri, new { codigo = "X" });
        HttpResponseMessage put = await client.PutAsJsonAsync(uri, new { codigo = "X" });
        HttpResponseMessage delete = await client.DeleteAsync(uri);

        // 405 é a resposta de quem tem a rota e não o verbo. Um 401/403 aqui significaria
        // que a rota de escrita existe e só está protegida — e uma proteção se afrouxa.
        post.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        put.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        delete.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Theory(DisplayName = "Versão inexistente da vendor MIME recusa com 406 em vez de servir a v1 em silêncio")]
    [InlineData("/api/configuracao/vocabularios/tipos-banca", "codigo-tipo-banca")]
    [InlineData("/api/configuracao/vocabularios/fases-canonicas", "codigo-fase-canonica")]
    public async Task Vocabularios_VersaoDesconhecidaDaVendorMime_Recusa(string rota, string resource)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(rota, UriKind.Relative));
        request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse($"application/vnd.uniplus.{resource}.v9+json"));

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
    }
}
