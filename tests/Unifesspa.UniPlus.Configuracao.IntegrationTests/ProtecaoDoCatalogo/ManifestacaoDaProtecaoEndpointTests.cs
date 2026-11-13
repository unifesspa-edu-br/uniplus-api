namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ProtecaoDoCatalogo;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// A manifestação técnica do Encarregado de dados na proteção do catálogo de tipos de documento e de fatos do candidato
/// pelo host real (UNI-REQ-0373, ADR-0163): quem tem a permissão de se manifestar registra o parecer num item com
/// aprovação solicitada, sem decidir.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class ManifestacaoDaProtecaoEndpointTests : ManifestacaoDaProtecaoTestBase
{
    public ManifestacaoDaProtecaoEndpointTests(ConfiguracaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Theory(DisplayName = "O Encarregado se manifesta no item solicitado, o detalhe mostra a vigente e o item continua solicitado")]
    [InlineData("TIPO_DOCUMENTO")]
    [InlineData("FATO")]
    public async Task Manifestar_ItemSolicitado_204(string cadastro)
    {
        await PrepararTodosAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await SolicitadoAsync(client, cadastro);

        (await ManifestarAsync(client, Kevin, cadastro, id, "INADEQUADA", "O laudo revela condição de saúde.")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        JsonElement detalhe = await DetalheAsync(client, cadastro, id);
        JsonElement aprovacao = detalhe.GetProperty("aprovacao");
        (aprovacao.GetProperty("situacao").GetString(), aprovacao.GetProperty("ciclo").GetInt32(), aprovacao.GetProperty("manifestacaoDesfavoravel").GetBoolean())
            .Should().Be(("SOLICITADA", 1, true));
        detalhe.GetProperty("manifestacoesVigentes").EnumerateArray().Select(m => (m.GetProperty("parecer").GetString(), m.GetProperty("autorNome").GetString()))
            .Should().Equal(("INADEQUADA", Kevin.Nome));
    }

    [Fact(DisplayName = "A forma inválida sai num único lote: parecer fora do vocabulário, ciclo ausente e cadastro inválido")]
    public async Task Manifestar_FormaInvalida_422EmLote()
    {
        await PrepararTodosAsync();
        using HttpClient client = Fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await ManifestarAsync(client, Kevin, "MODALIDADE", Guid.NewGuid(), "TALVEZ", ciclo: null);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").EnumerateArray()
            .Select(e => e.GetProperty("field").GetString()).Should().BeEquivalentTo("cadastro", "ciclo", "parecer");
    }

    [Fact(DisplayName = "Os pareceres desfavoráveis exigem o texto, e o texto longo demais é recusado")]
    public async Task Manifestar_Texto_422()
    {
        await PrepararTodosAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await SolicitadoAsync(client, "TIPO_DOCUMENTO");

        HttpResponseMessage semTexto = await ManifestarAsync(client, Kevin, "TIPO_DOCUMENTO", id, "RECOMENDA_AJUSTE", texto: null);
        HttpResponseMessage longo = await ManifestarAsync(client, Kevin, "TIPO_DOCUMENTO", id, "ADEQUADA", texto: new string('a', 2001));

        (await CodigoAsync(semTexto)).Should().Be("uniplus.configuracao.protecao_do_catalogo.texto_da_manifestacao_obrigatorio");
        (await CodigoAsync(longo)).Should().Be("uniplus.configuracao.protecao_do_catalogo.texto_da_manifestacao_muito_longo");
    }

    [Fact(DisplayName = "O item pendente recusa a manifestação, e o inexistente responde 404")]
    public async Task Manifestar_ItemForaDeSolicitacaoEInexistente()
    {
        await PrepararTodosAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid pendente = await CadastrarTipoAsync(client, $"{Marca()} Certidão de nascimento");

        HttpResponseMessage foraDeSolicitacao = await ManifestarAsync(client, Kevin, "TIPO_DOCUMENTO", pendente, "ADEQUADA");
        HttpResponseMessage inexistente = await ManifestarAsync(client, Kevin, "FATO", Guid.NewGuid(), "ADEQUADA");

        (await CodigoAsync(foraDeSolicitacao)).Should().Be("uniplus.configuracao.protecao_do_catalogo.item_fora_de_solicitacao");
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "A manifestação com o ciclo que o item não tem responde 409 sem nova tentativa")]
    public async Task Manifestar_CicloMudou_409()
    {
        await PrepararTodosAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid solicitado = await SolicitadoAsync(client, "FATO");

        HttpResponseMessage cicloErrado = await ManifestarAsync(client, Kevin, "FATO", solicitado, "ADEQUADA", ciclo: 2);

        cicloErrado.StatusCode.Should().Be(HttpStatusCode.Conflict);
        JsonElement problema = await cicloErrado.Content.ReadFromJsonAsync<JsonElement>();
        problema.GetProperty("code").GetString().Should().Be("uniplus.configuracao.protecao_do_catalogo.ciclo_mudou");
        problema.TryGetProperty("retryable", out _).Should().BeFalse();
    }

    [Fact(DisplayName = "Sem Idempotency-Key a manifestação é recusada com 400")]
    public async Task Manifestar_SemChave_400()
    {
        await PrepararTodosAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri($"{Base}/manifestacoes", UriKind.Relative))
        {
            Content = JsonContent.Create(new { cadastro = "FATO", itemId = Guid.NewGuid(), ciclo = 1, parecer = "ADEQUADA" }),
        };
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, Kevin.Sub);

        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "O Representante do controlador não se manifesta, e o Encarregado não decide")]
    public async Task Permissoes_RepresentanteNaoSeManifestaEEncarregadoNaoDecide()
    {
        await PrepararTodosAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await SolicitadoAsync(client, "TIPO_DOCUMENTO");

        HttpResponseMessage representante = await ManifestarAsync(client, Betina, "TIPO_DOCUMENTO", id, "ADEQUADA");
        HttpResponseMessage encarregado = await DecidirComoAsync(client, Kevin, "TIPO_DOCUMENTO", id, "APROVAR");

        representante.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        encarregado.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await DetalheAsync(client, "TIPO_DOCUMENTO", id)).GetProperty("aprovacao").GetProperty("situacao").GetString().Should().Be("SOLICITADA");
    }

    [Fact(DisplayName = "A nova manifestação da mesma pessoa substitui a vigente; a de outra pessoa coexiste")]
    public async Task Manifestar_SubstituicaoECoexistencia()
    {
        await PrepararTodosAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await SolicitadoAsync(client, "FATO");

        (await ManifestarAsync(client, Kevin, "FATO", id, "INADEQUADA")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ManifestarAsync(client, Kevin, "FATO", id, "ADEQUADA", texto: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ManifestarAsync(client, Lucia, "FATO", id, "RECOMENDA_AJUSTE")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        JsonElement detalhe = await DetalheAsync(client, "FATO", id);
        detalhe.GetProperty("manifestacoesVigentes").EnumerateArray().Select(m => (m.GetProperty("parecer").GetString(), m.GetProperty("autorNome").GetString()))
            .Should().Equal(("ADEQUADA", Kevin.Nome), ("RECOMENDA_AJUSTE", Lucia.Nome));
        detalhe.GetProperty("aprovacao").GetProperty("manifestacaoDesfavoravel").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName = "Editar o item solicitado sem reiniciar mantém o ciclo e as manifestações vigentes")]
    public async Task EdicaoSemReinicio_MantemAsVigentes()
    {
        await PrepararTodosAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await SolicitadoAsync(client, "FATO");
        (await ManifestarAsync(client, Kevin, "FATO", id, "INADEQUADA")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await EditarFatoAsync(client, id)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        JsonElement detalhe = await DetalheAsync(client, "FATO", id);
        (detalhe.GetProperty("aprovacao").GetProperty("ciclo").GetInt32(), detalhe.GetProperty("manifestacoesVigentes").GetArrayLength()).Should().Be((1, 1));
    }

    [Theory(DisplayName = "Editar o item reprovado ou com ajuste recomendado reinicia o ciclo e encerra as manifestações dele")]
    [InlineData("REPROVAR", null)]
    [InlineData("RECOMENDAR_AJUSTE", "SENSIVEL")]
    public async Task EdicaoComReinicio_EncerraOCiclo(string decisao, string? recomendada)
    {
        await PrepararTodosAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await SolicitadoAsync(client, "FATO");
        (await ManifestarAsync(client, Kevin, "FATO", id, "INADEQUADA")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await DecidirComoAsync(client, Betina, "FATO", id, decisao, "O dado é sensível.", recomendada)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await EditarFatoAsync(client, id)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        JsonElement detalhe = await DetalheAsync(client, "FATO", id);
        (detalhe.GetProperty("aprovacao").GetProperty("ciclo").GetInt32(), detalhe.GetProperty("manifestacoesVigentes").GetArrayLength()).Should().Be((2, 0));
        (await HistoricoAsync(client, Kevin, "FATO", id)).Should().ContainSingle().Which.GetProperty("situacao").GetString().Should().Be("ENCERRADA");
    }

    private static Task<HttpResponseMessage> EditarFatoAsync(HttpClient client, Guid id) =>
        Enviar(client, HttpMethod.Put, $"{Fatos}/{id}", Isabella, new { nome = $"{Marca()} Renda familiar", descricao = "Renda bruta declarada pelo candidato." });
}
