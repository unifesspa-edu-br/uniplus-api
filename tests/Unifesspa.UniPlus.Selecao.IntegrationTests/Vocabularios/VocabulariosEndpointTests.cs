namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Vocabularios;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

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
        FundamentoIsencaoDto[] fundamentos = await LerVocabularioAsync<FundamentoIsencaoDto>(
            "/api/selecao/fundamentos-isencao", "application/vnd.uniplus.fundamento-isencao.v1+json");

        // A lista é afirmada por extenso, e não derivada do enum, de propósito:
        // é o contrato publicado que está sob teste, e derivá-lo da mesma fonte
        // que o produz faria o teste concordar com qualquer mudança.
        fundamentos.Select(f => f.Codigo).Should()
            .Equal("CADASTRO_UNICO", "DOACAO_MEDULA_OSSEA", "CARENCIA_SOCIOECONOMICA");
        fundamentos.Should().AllSatisfy(f =>
        {
            f.Nome.Should().NotBeNullOrWhiteSpace();
            f.Descricao.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact(DisplayName = "GET /campos-divulgacao marca o número de inscrição como obrigatório e o nome completo como dependente de justificativa")]
    public async Task ListarCamposDivulgacao_DevolveFlagsQueDecidemATela()
    {
        CampoDivulgacaoDto[] campos = await LerVocabularioAsync<CampoDivulgacaoDto>(
            "/api/selecao/campos-divulgacao", "application/vnd.uniplus.campo-divulgacao.v1+json");

        // A ordem vai do piso à forma mais identificadora do candidato — é a leitura que
        // importa para quem decide o que publicar, e não a alfabética.
        campos.Select(c => c.Codigo).Should().Equal(
            ConfiguracaoDivulgacao.NumeroInscricao,
            ConfiguracaoDivulgacao.NomeAbreviado,
            ConfiguracaoDivulgacao.Nome);

        AfirmarFlagsDeObrigatoriedadeEJustificativa(campos);
    }

    [Theory(DisplayName = "As leituras de vocabulário devolvem código e rótulo, na ordem do enum, sem exigir autenticação")]
    [InlineData("/api/selecao/vocabularios/formas-comprovacao", "forma-de-comprovacao", new[] { "DOCUMENTO", "TODOS_OS_ITENS", "QUANTIDADE_MINIMA_DE_ITENS" })]
    [InlineData("/api/selecao/unidades-do-prazo-de-recurso", "unidade-do-prazo-de-recurso", new[] { "horas", "diasUteis" })]
    [InlineData("/api/selecao/efeitos-suspensivos", "efeito-suspensivo", new[] { "semEfeitoSuspensivo" })]
    [InlineData("/api/selecao/guardas-da-vaga", "guarda-da-vaga", new[] { "ateOResultadoDefinitivo", "naoGuarda" })]
    [InlineData("/api/selecao/vocabularios/modos-deliberacao", "modo-deliberacao", new[] { "colegiadaComVotacao", "atoIndividual" })]
    [InlineData("/api/selecao/vocabularios/criterios-votacao", "criterio-votacao", new[] { "maioriaSimples", "maioriaAbsoluta", "unanimidade" })]
    [InlineData("/api/selecao/vocabularios/desempates-votacao", "desempate-votacao", new[] { "votoDaPresidencia", "novaVotacao" })]
    [InlineData("/api/selecao/vocabularios/encerramentos-votacao", "encerramento-votacao", new[] { "todosOsTitulares", "presidenciaNoQuorum" })]
    [InlineData("/api/selecao/vocabularios/resultados-sem-unanimidade", "resultado-sem-unanimidade", new[] { "favoravel", "desfavoravel", "novaVotacao" })]
    public async Task Vocabularios_DevolvemCodigoERotulo(string rota, string recurso, string[] codigos)
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri(rota, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be($"application/vnd.uniplus.{recurso}.v1+json");
        using JsonDocument corpo = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement[] opcoes = [.. corpo.RootElement.EnumerateArray()];
        opcoes.Select(static o => o.GetProperty("codigo").GetString()).Should().Equal(codigos);
        opcoes.Should().AllSatisfy(static o => o.GetProperty("rotulo").GetString().Should().NotBeNullOrWhiteSpace());
    }

    [Theory(DisplayName = "As opções de efeito suspensivo e de guarda da vaga devolvem a descrição do que cada uma significa")]
    [InlineData("/api/selecao/efeitos-suspensivos", "efeito-suspensivo")]
    [InlineData("/api/selecao/guardas-da-vaga", "guarda-da-vaga")]
    public async Task VocabulariosDoRecursoComDescricao_DevolvemDescricaoPreenchida(string rota, string recurso)
    {
        OpcaoComDescricao[] opcoes = await LerVocabularioAsync<OpcaoComDescricao>(rota, $"application/vnd.uniplus.{recurso}.v1+json");

        opcoes.Should().NotBeEmpty();
        opcoes.Should().AllSatisfy(static o => o.Descricao.Should().NotBeNullOrWhiteSpace($"a opção '{o.Codigo}' precisa explicar o que significa"));
    }

    [Theory(DisplayName = "A leitura de vocabulário com versão inexistente da vendor MIME recusa com 406")]
    [InlineData("/api/selecao/vocabularios/formas-comprovacao", "forma-de-comprovacao")]
    [InlineData("/api/selecao/unidades-do-prazo-de-recurso", "unidade-do-prazo-de-recurso")]
    [InlineData("/api/selecao/efeitos-suspensivos", "efeito-suspensivo")]
    [InlineData("/api/selecao/guardas-da-vaga", "guarda-da-vaga")]
    [InlineData("/api/selecao/vocabularios/modos-deliberacao", "modo-deliberacao")]
    [InlineData("/api/selecao/vocabularios/criterios-votacao", "criterio-votacao")]
    [InlineData("/api/selecao/vocabularios/desempates-votacao", "desempate-votacao")]
    [InlineData("/api/selecao/vocabularios/encerramentos-votacao", "encerramento-votacao")]
    [InlineData("/api/selecao/vocabularios/resultados-sem-unanimidade", "resultado-sem-unanimidade")]
    public async Task Vocabularios_VersaoDesconhecida_Recusa(string rota, string recurso)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(rota, UriKind.Relative));
        request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse($"application/vnd.uniplus.{recurso}.v9+json"));

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
    }

    [Theory(DisplayName = "Vocabulário governado por código não tem rota de escrita — evolução é mudança versionada da API, não cadastro")]
    [InlineData("/api/selecao/fundamentos-isencao")]
    [InlineData("/api/selecao/campos-divulgacao")]
    [InlineData("/api/selecao/vocabularios/formas-comprovacao")]
    [InlineData("/api/selecao/unidades-do-prazo-de-recurso")]
    [InlineData("/api/selecao/efeitos-suspensivos")]
    [InlineData("/api/selecao/guardas-da-vaga")]
    [InlineData("/api/selecao/vocabularios/modos-deliberacao")]
    [InlineData("/api/selecao/vocabularios/criterios-votacao")]
    [InlineData("/api/selecao/vocabularios/desempates-votacao")]
    [InlineData("/api/selecao/vocabularios/encerramentos-votacao")]
    [InlineData("/api/selecao/vocabularios/resultados-sem-unanimidade")]
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

    private static void AfirmarFlagsDeObrigatoriedadeEJustificativa(CampoDivulgacaoDto[] campos)
    {
        campos.Single(c => c.Codigo == ConfiguracaoDivulgacao.NumeroInscricao)
            .Obrigatorio.Should().BeTrue("é o piso da divulgação e nenhuma configuração o remove");
        campos.Single(c => c.Codigo == ConfiguracaoDivulgacao.Nome)
            .ExigeJustificativa.Should().BeTrue("publicar o nome integral exige justificativa (UNI-REQ-0050)");
        campos.Single(c => c.Codigo == ConfiguracaoDivulgacao.NomeAbreviado)
            .ExigeJustificativa.Should().BeFalse();
    }

    private async Task<T[]> LerVocabularioAsync<T>(string rota, string mediaTypeEsperada)
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri(rota, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be(mediaTypeEsperada);
        return (await response.Content.ReadFromJsonAsync<T[]>())!;
    }

    private sealed record OpcaoComDescricao(string Codigo, string? Descricao);
}
