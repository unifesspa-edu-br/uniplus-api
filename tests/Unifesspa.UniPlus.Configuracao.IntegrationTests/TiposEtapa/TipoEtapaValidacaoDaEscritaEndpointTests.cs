namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposEtapa;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposEtapa.ApoioDoEndpointDeTiposEtapa;

/// <summary>A recusa de escrita inválida dos tipos de etapa: texto que o banco não grava, violações acumuladas e validação antes da busca.</summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class TipoEtapaValidacaoDaEscritaEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public TipoEtapaValidacaoDaEscritaEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory(DisplayName = "POST recusa U+0000 nos campos textuais com 422")]
    [InlineData("codigo")]
    [InlineData("nome")]
    [InlineData("descricao")]
    public async Task Criar_CampoTextualComCaractereNulo_Retorna422(string campo)
    {
        string invalido = $"valor{(char)0}invalido";
        object payload = campo switch
        {
            "codigo" => new { codigo = invalido, nome = "Nome válido", descricao = "Descrição válida", avaliacao = new { conceitosAdmitidos = Array.Empty<string>(), admitePontuacao = true, admiteEliminacao = true, exigeCorrecaoIndependente = false } },
            "nome" => new { codigo = CodigoUnico(), nome = invalido, descricao = "Descrição válida", avaliacao = new { conceitosAdmitidos = Array.Empty<string>(), admitePontuacao = true, admiteEliminacao = true, exigeCorrecaoIndependente = false } },
            "descricao" => new { codigo = CodigoUnico(), nome = "Nome válido", descricao = invalido, avaliacao = new { conceitosAdmitidos = Array.Empty<string>(), admitePontuacao = true, admiteEliminacao = true, exigeCorrecaoIndependente = false } },
            _ => throw new InvalidOperationException($"Campo de teste inesperado: {campo}"),
        };
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
        HttpResponseMessage criar = await EnviarPostAdmin(client, Corpo(("codigo", CodigoUnico())));
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage response = await EnviarPutAdmin(client, id, Corpo(("id", id), (campo, $"valor{(char)0}invalido")));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "ADR-0125: POST com código e nome ausentes ao mesmo tempo devolve as duas violações em errors[], campo em camelCase")]
    public async Task Criar_CodigoENomeAusentes_DevolveAsDuasViolacoesEmErrors()
    {
        var body = new { codigo = "", nome = "", avaliacao = new { conceitosAdmitidos = Array.Empty<string>(), admitePontuacao = true, admiteEliminacao = true, exigeCorrecaoIndependente = false } };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.configuracao.tipo_etapa.codigo_obrigatorio");

        JsonElement errors = doc.RootElement.GetProperty("errors");
        errors.GetArrayLength().Should().Be(2);
        errors[0].GetProperty("field").GetString().Should().Be("codigo");
        errors[1].GetProperty("field").GetString().Should().Be("nome");
    }

    /// <summary>
    /// ADR-0125: prova que "codigo" genuinamente ausente do JSON (não string
    /// vazia) chega ao domínio como 422 específico, não ao 400 genérico do
    /// ASP.NET — só possível porque
    /// <see cref="Application.Commands.TiposEtapa.CriarTipoEtapaCommand.Codigo"/>
    /// é <c>string?</c>, não <c>string</c>.
    /// </summary>
    [Fact(DisplayName = "ADR-0125: POST com código genuinamente ausente do JSON chega ao domínio como 422 específico")]
    public async Task Criar_CodigoAusenteDoJson_ChegaAoDominioComoViolacaoEspecifica()
    {
        const string json = """{"nome":"Nome válido"}""";

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-etapa", UriKind.Relative));
        AutenticarComoAdmin(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.configuracao.tipo_etapa.codigo_obrigatorio");
    }

    [Fact(DisplayName = "PUT com Id inexistente e Nome vazio devolve 422 (validação vence sobre 404)")]
    public async Task Atualizar_IdInexistenteENomeVazio_Retorna422()
    {
        Guid id = Guid.NewGuid();

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPutAdmin(client, id, new { id, nome = "", avaliacao = new { conceitosAdmitidos = Array.Empty<string>(), admitePontuacao = true, admiteEliminacao = true, exigeCorrecaoIndependente = false } });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
