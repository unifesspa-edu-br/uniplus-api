namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposProcesso;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposProcesso.ConferenciaDeTiposProcesso;
using static Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposProcesso.RequisicoesDeTipoProcesso;


[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class TipoProcessoDesativacaoEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public TipoProcessoDesativacaoEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "DELETE desativa, remove da leitura pública e mantém o código reservado")]
    public async Task Desativar_OcultaDaLeituraPublicaESemReusoDoCodigo()
    {
        string codigo = CodigoUnico();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage criar = await EnviarPostAdmin(client, new { codigo, nome = "Seleção temporária" });
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage desativar = await EnviarDeleteAdmin(client, id);
        desativar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage obter = await client.GetAsync(new Uri($"/api/configuracao/tipos-processo/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.NotFound, "a API pública só expõe itens ativos");

        HttpResponseMessage recriar = await EnviarPostAdmin(client, new { codigo, nome = "Tentativa de reuso" });
        recriar.StatusCode.Should().Be(HttpStatusCode.Conflict, "desativar não libera a identidade regulatória do código");
    }

    [Fact(DisplayName = "POST .../ativacao sem autenticação retorna 401")]
    public async Task Reativar_SemAutenticacao_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri($"/api/configuracao/admin/tipos-processo/{Guid.NewGuid()}/ativacao", UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST .../ativacao devolve o tipo desativado à leitura pública com código e nome intactos")]
    public async Task Reativar_TipoDesativado_VoltaAApareceNaLeituraPublica()
    {
        string codigo = CodigoUnico();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage criar = await EnviarPostAdmin(client, new { codigo, nome = "Seleção reativável", descricao = "Descrição preservada" });
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        (await EnviarDeleteAdmin(client, id)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync(new Uri($"/api/configuracao/tipos-processo/{id}", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        HttpResponseMessage reativar = await EnviarAtivacaoAdmin(client, id);

        reativar.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await ConferirTipoReativado(client, id, codigo);
    }

    [Fact(DisplayName = "POST .../ativacao sobre tipo já ativo retorna 422 com o código de erro específico")]
    public async Task Reativar_TipoJaAtivo_Retorna422()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, new { codigo = CodigoUnico(), nome = "Seleção ativa" });
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage response = await EnviarAtivacaoAdmin(client, id);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.configuracao.tipo_processo.ja_ativo");
    }

    [Fact(DisplayName = "POST .../ativacao com Id inexistente retorna 404")]
    public async Task Reativar_IdInexistente_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await EnviarAtivacaoAdmin(client, Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

}
