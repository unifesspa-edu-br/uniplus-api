namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposProcesso;

using System.Net;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposProcesso.ConferenciaDeTiposProcesso;
using static Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposProcesso.RequisicoesDeTipoProcesso;


[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class TipoProcessoCadastroEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public TipoProcessoCadastroEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "POST /api/configuracao/admin/tipos-processo sem autenticação retorna 401")]
    public async Task Criar_SemAutenticacao_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-processo", UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST cria (201), GET público lista e PUT altera somente os dados descritivos")]
    public async Task Criar_ListarEAtualizar_ComAdmin_PersisteCadastro()
    {
        string codigo = CodigoUnico();
        using HttpClient client = _fixture.Factory.CreateClient();

        Guid id = await CriarTipoInicial(client, codigo);

        await ConferirTipoNaListagemPublica(client, id, codigo);
        await AtualizarEConferirTipo(client, id, codigo);
    }

    [Theory(DisplayName = "POST recusa U+0000 nos campos textuais com 422")]
    [InlineData("codigo")]
    [InlineData("nome")]
    [InlineData("descricao")]
    public async Task Criar_CampoTextualComCaractereNulo_Retorna422(string campo)
    {
        object payload = CamposDeCriacaoComCaractereNulo(campo);
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await EnviarPostAdmin(client, payload);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Theory(DisplayName = "PUT recusa U+0000 nos campos editáveis com 422")]
    [InlineData("nome")]
    [InlineData("descricao")]
    public async Task Atualizar_CampoTextualComCaractereNulo_Retorna422(string campo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarTipoComCamposValidos(client);
        object payload = CamposDeAtualizacaoComCaractereNulo(id, campo);

        HttpResponseMessage response = await EnviarPutAdmin(client, id, payload);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "ADR-0125: POST com código e nome ausentes ao mesmo tempo devolve as duas violações em errors[], campo em camelCase")]
    public async Task Criar_CodigoENomeAusentes_DevolveAsDuasViolacoesEmErrors()
    {
        var body = new { codigo = "", nome = "" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        await ConferirViolacoesDeCodigoENome(response);
    }

    [Fact(DisplayName = "ADR-0125: POST com código genuinamente ausente do JSON chega ao domínio como 422 específico")]
    public async Task Criar_CodigoAusenteDoJson_ChegaAoDominioComoViolacaoEspecifica()
    {
        const string json = """{"nome":"Nome válido"}""";

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarJsonAdmin(client, json);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.configuracao.tipo_processo.codigo_obrigatorio");
    }

    [Fact(DisplayName = "PUT com Id inexistente e Nome vazio devolve 422 (validação vence sobre 404)")]
    public async Task Atualizar_IdInexistenteENomeVazio_Retorna422()
    {
        Guid id = Guid.NewGuid();

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPutAdmin(client, id, new { id, nome = "" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

}
