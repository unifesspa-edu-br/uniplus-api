namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposEtapa;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposEtapa.ApoioDoEndpointDeTiposEtapa;

/// <summary>Contrato HTTP da manutenção dos tipos de etapa (UNI-REQ-0087): criar, atualizar só os dados descritivos e desativar mantendo o código reservado.</summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class TipoEtapaManutencaoEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public TipoEtapaManutencaoEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "POST /api/configuracao/admin/tipos-etapa sem autenticação retorna 401")]
    public async Task Criar_SemAutenticacao_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-etapa", UriKind.Relative));
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
        Guid id = await CriarSelecaoDeTeste(client, codigo);

        await ConferirListadoComCodigo(client, id, codigo);

        HttpResponseMessage atualizar = await EnviarPutAdmin(client, id, Corpo(
            ("id", id), ("nome", "Seleção renomeada"), ("descricao", "Descrição atualizada"), ("admitePontuacao", false)));

        atualizar.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await ConferirRenomeado(client, id, codigo);
    }

    private static async Task<Guid> CriarSelecaoDeTeste(HttpClient client, string codigo)
    {
        HttpResponseMessage criar = await EnviarPostAdmin(client, Corpo(("codigo", codigo), ("nome", "Seleção de teste"), ("descricao", "Descrição inicial")));

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        id.Should().NotBe(Guid.Empty);
        return id;
    }

    private static async Task ConferirListadoComCodigo(HttpClient client, Guid id, string codigo)
    {
        HttpResponseMessage listar = await client.GetAsync(new Uri("/api/configuracao/tipos-etapa", UriKind.Relative));
        listar.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument lista = JsonDocument.Parse(await listar.Content.ReadAsStringAsync());
        lista.RootElement.EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == id)
            .GetProperty("codigo").GetString().Should().Be(codigo);
    }

    private static async Task ConferirRenomeado(HttpClient client, Guid id, string codigo)
    {
        using JsonDocument item = await ObterTipo(client, id);
        item.RootElement.GetProperty("codigo").GetString().Should().Be(codigo, "o código é a identidade imutável do tipo de etapa");
        item.RootElement.GetProperty("nome").GetString().Should().Be("Seleção renomeada");
        item.RootElement.TryGetProperty("_links", out _).Should().BeTrue("a leitura pública expõe HATEOAS nível 1");
    }

    [Fact(DisplayName = "DELETE desativa, remove da leitura pública e mantém o código reservado")]
    public async Task Desativar_OcultaDaLeituraPublicaESemReusoDoCodigo()
    {
        string codigo = CodigoUnico();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage criar = await EnviarPostAdmin(client, new { codigo, nome = "Seleção temporária", avaliacao = new { conceitosAdmitidos = Array.Empty<string>(), admitePontuacao = true, admiteEliminacao = true, exigeCorrecaoIndependente = false } });
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage desativar = await EnviarDeleteAdmin(client, id);
        desativar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage obter = await client.GetAsync(new Uri($"/api/configuracao/tipos-etapa/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.NotFound, "a API pública só expõe itens ativos");

        HttpResponseMessage recriar = await EnviarPostAdmin(client, new { codigo, nome = "Tentativa de reuso", avaliacao = new { conceitosAdmitidos = Array.Empty<string>(), admitePontuacao = true, admiteEliminacao = true, exigeCorrecaoIndependente = false } });
        recriar.StatusCode.Should().Be(HttpStatusCode.Conflict, "desativar não libera a identidade regulatória do código");
    }

    [Fact(DisplayName = "POST não define a nota de origem no ENEM: o campo enviado é ignorado")]
    public async Task Criar_ComNotaDeOrigemNoEnem_Ignora()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage criar = await EnviarPostAdmin(client, Corpo(("codigo", CodigoUnico()), ("notaDeOrigemNoEnem", true)));

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        using JsonDocument item = await ObterTipo(client, await criar.Content.ReadFromJsonAsync<Guid>());
        item.RootElement.GetProperty("avaliacao").GetProperty("notaDeOrigemNoEnem").GetBoolean().Should().BeFalse(
            "a origem da nota vem só da carga do cadastro");
    }
}
