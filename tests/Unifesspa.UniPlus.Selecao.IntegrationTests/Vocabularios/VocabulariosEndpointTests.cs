namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Vocabularios;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AwesomeAssertions;

using Outbox.Cascading;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Contrato HTTP dos dois vocabulários fechados da configuração (UNI-REQ-0050/0101). O que
/// se prova aqui é o que o fitness test não alcança: que as rotas existem, respondem sem
/// autenticação, negociam a vendor MIME e não têm par de escrita.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class VocabulariosEndpointTests
{
    private readonly CascadingFixture _fixture;

    public VocabulariosEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /fundamentos-isencao devolve os dois fundamentos com código, nome e descrição, sem exigir autenticação")]
    public async Task ListarFundamentos_DevolveVocabularioCompleto()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/selecao/fundamentos-isencao", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/vnd.uniplus.fundamento-isencao.v1+json");

        FundamentoIsencaoDto[] fundamentos =
            (await response.Content.ReadFromJsonAsync<FundamentoIsencaoDto[]>())!;

        fundamentos.Select(f => f.Codigo).Should().Equal("CADASTRO_UNICO", "DOACAO_MEDULA_OSSEA");
        fundamentos.Should().AllSatisfy(f =>
        {
            f.Nome.Should().NotBeNullOrWhiteSpace();
            f.Descricao.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact(DisplayName = "GET /campos-divulgacao marca o número de inscrição como obrigatório e o nome completo como dependente de justificativa")]
    public async Task ListarCamposDivulgacao_DevolveFlagsQueDecidemATela()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/selecao/campos-divulgacao", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/vnd.uniplus.campo-divulgacao.v1+json");

        CampoDivulgacaoDto[] campos =
            (await response.Content.ReadFromJsonAsync<CampoDivulgacaoDto[]>())!;

        // A ordem vai do piso à forma mais identificadora do candidato — é a leitura que
        // importa para quem decide o que publicar, e não a alfabética.
        campos.Select(c => c.Codigo).Should().Equal(
            ConfiguracaoDivulgacao.NumeroInscricao,
            ConfiguracaoDivulgacao.NomeAbreviado,
            ConfiguracaoDivulgacao.Nome);

        campos.Single(c => c.Codigo == ConfiguracaoDivulgacao.NumeroInscricao)
            .Obrigatorio.Should().BeTrue("é o piso da divulgação e nenhuma configuração o remove");
        campos.Single(c => c.Codigo == ConfiguracaoDivulgacao.Nome)
            .ExigeJustificativa.Should().BeTrue("publicar o nome integral exige justificativa (UNI-REQ-0050)");
        campos.Single(c => c.Codigo == ConfiguracaoDivulgacao.NomeAbreviado)
            .ExigeJustificativa.Should().BeFalse();
    }

    [Theory(DisplayName = "Vocabulário governado por código não tem rota de escrita — evolução é mudança versionada da API, não cadastro")]
    [InlineData("/api/selecao/fundamentos-isencao")]
    [InlineData("/api/selecao/campos-divulgacao")]
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

    [Fact(DisplayName = "Versão inexistente da vendor MIME recusa com 406 em vez de servir a v1 em silêncio")]
    public async Task Vocabularios_VersaoDesconhecidaDaVendorMime_Recusa()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri("/api/selecao/fundamentos-isencao", UriKind.Relative));
        request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/vnd.uniplus.fundamento-isencao.v9+json"));

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
    }
}
