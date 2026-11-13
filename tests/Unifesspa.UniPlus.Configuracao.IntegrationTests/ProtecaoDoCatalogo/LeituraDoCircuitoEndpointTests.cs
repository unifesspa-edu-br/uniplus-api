namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ProtecaoDoCatalogo;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// A manifestação e a decisão valem sobre o que quem age leu do item (UNI-REQ-0374): a manifestação registrada entre a
/// leitura e o envio da decisão a recusa por conflito; a decisão tomada entre a leitura e o envio da manifestação a recusa
/// por conflito. E o limite de 2.000 caracteres conta caracteres, não unidades UTF-16.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class LeituraDoCircuitoEndpointTests : ManifestacaoDaProtecaoTestBase
{
    private const string Justificativa = "A classificação declarada basta: o documento só comprova a matrícula.";

    public LeituraDoCircuitoEndpointTests(ConfiguracaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Theory(DisplayName = "A manifestação registrada depois da leitura recusa a decisão com 409 sem nova tentativa; relido o item, a decisão passa")]
    [InlineData("TIPO_DOCUMENTO")]
    [InlineData("FATO")]
    public async Task ManifestacaoDepoisDaLeitura_RecusaADecisao(string cadastro)
    {
        await PrepararTodosAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await SolicitadoAsync(client, cadastro);
        LeituraDoCircuitoLida lida = await LerOCircuitoAsync(client, Betina, cadastro, id);
        (await ManifestarAsync(client, Kevin, cadastro, id, "ADEQUADA", texto: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage decisao = await DecidirSobreALeituraAsync(
            client, new(cadastro, id, "APROVAR", Classificacao(cadastro), Justificativa: Justificativa), lida);

        await DeveSerConflitoSemNovaTentativaAsync(decisao, "manifestacao_nao_lida");
        (await DecidirComoAsync(client, Betina, cadastro, id, "APROVAR")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Theory(DisplayName = "A decisão tomada depois da leitura recusa a manifestação com 409 sem nova tentativa")]
    [InlineData("TIPO_DOCUMENTO")]
    [InlineData("FATO")]
    public async Task DecisaoDepoisDaLeitura_RecusaAManifestacao(string cadastro)
    {
        await PrepararTodosAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await SolicitadoAsync(client, cadastro);
        LeituraDoCircuitoLida lida = await LerOCircuitoAsync(client, Kevin, cadastro, id);
        (await DecidirComoAsync(client, Betina, cadastro, id, "REPROVAR", "O dado é desnecessário.")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage manifestacao = await ManifestarAsync(client, Kevin, cadastro, id, "INADEQUADA", ciclo: lida.Ciclo);

        await DeveSerConflitoSemNovaTentativaAsync(manifestacao, "decisao_ja_tomada");
    }

    [Fact(DisplayName = "A proteção editada e a manifestação registrada depois da leitura chegam juntas ao errors[] do 409, com protecao_mudou no topo")]
    public async Task ProtecaoEManifestacaoDepoisDaLeitura_ErrorsComOsDoisCodigos()
    {
        await PrepararTodosAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await SolicitadoAsync(client, "TIPO_DOCUMENTO");
        LeituraDoCircuitoLida lida = await LerOCircuitoAsync(client, Betina, "TIPO_DOCUMENTO", id);
        (await ManifestarAsync(client, Kevin, "TIPO_DOCUMENTO", id, "ADEQUADA", texto: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage resposta = await DecidirSobreALeituraAsync(client, new("TIPO_DOCUMENTO", id, "APROVAR", "SENSIVEL"), lida);

        JsonElement problema = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        (resposta.StatusCode, problema.GetProperty("code").GetString()).Should().Be(
            (HttpStatusCode.Conflict, "uniplus.configuracao.protecao_do_catalogo.protecao_mudou"));
        problema.GetProperty("errors").EnumerateArray().Select(static e => (e.GetProperty("field").GetString(), e.GetProperty("code").GetString())).Should().Equal(
            ("classificacaoProtecao", "uniplus.configuracao.protecao_do_catalogo.protecao_mudou"),
            ("manifestacoesNoCiclo", "uniplus.configuracao.protecao_do_catalogo.manifestacao_nao_lida"));
    }

    [Fact(DisplayName = "A decisão sem o ciclo e a contagem de manifestações lidos é recusada nos dois campos")]
    public async Task DecisaoSemLeitura_422()
    {
        await PrepararTodosAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await SolicitadoAsync(client, "TIPO_DOCUMENTO");

        HttpResponseMessage resposta = await Enviar(client, HttpMethod.Post, Base, Betina, AprovacaoSemLeitura(id));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").EnumerateArray()
            .Select(static e => e.GetProperty("field").GetString()).Should().BeEquivalentTo("ciclo", "manifestacoesNoCiclo");
    }

    [Fact(DisplayName = "O texto com 2.000 emojis é aceito e relido inteiro; com 2.001, recusado")]
    public async Task TextoComEmojis_ContaCaracteres()
    {
        await PrepararTodosAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await SolicitadoAsync(client, "FATO");
        string noLimite = string.Concat(Enumerable.Repeat("\U0001F512", 2000));

        HttpResponseMessage acima = await ManifestarAsync(client, Kevin, "FATO", id, "INADEQUADA", noLimite + "\U0001F512");
        HttpResponseMessage noLimiteResposta = await ManifestarAsync(client, Kevin, "FATO", id, "INADEQUADA", noLimite);

        (await CodigoAsync(acima)).Should().Be("uniplus.configuracao.protecao_do_catalogo.texto_da_manifestacao_muito_longo");
        noLimiteResposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await HistoricoAsync(client, Kevin, "FATO", id)).Single().GetProperty("texto").GetString().Should().Be(noLimite);
    }

    [Fact(DisplayName = "A justificativa com 2.000 emojis é aceita e registrada no ato da aprovação")]
    public async Task JustificativaComEmojis_Aceita()
    {
        await PrepararTodosAsync();
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await SolicitadoAsync(client, "TIPO_DOCUMENTO");
        string noLimite = string.Concat(Enumerable.Repeat("\U0001F512", 2000));

        (await DecidirComoAsync(client, Betina, "TIPO_DOCUMENTO", id, "APROVAR", noLimite)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await UltimoAtoAsync(client, "TIPO_DOCUMENTO", id)).GetProperty("justificativa").GetString().Should().Be(noLimite);
    }

    private static object AprovacaoSemLeitura(Guid id) => new
    {
        cadastro = "TIPO_DOCUMENTO",
        itemId = id,
        decisao = "APROVAR",
        classificacaoProtecao = "INTERNO",
        hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
        finalidadeTratamento = Finalidade,
    };

    private static async Task DeveSerConflitoSemNovaTentativaAsync(HttpResponseMessage resposta, string codigo)
    {
        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict, await resposta.Content.ReadAsStringAsync());
        JsonElement problema = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        problema.GetProperty("code").GetString().Should().Be($"uniplus.configuracao.protecao_do_catalogo.{codigo}");
        problema.TryGetProperty("retryable", out _).Should().BeFalse();
    }
}
