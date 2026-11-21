namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposConceito;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure.TiposConceitoHttpDeTeste;

/// <summary>Manutenção do catálogo de tipos de conceito: criação, edição e desativação, com o código reservado para sempre.</summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class TipoConceitoManutencaoEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public TipoConceitoManutencaoEndpointTests(ConfiguracaoEndpointFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "POST sem autenticação retorna 401")]
    public async Task Criar_SemAutenticacao_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage requisicao = new(HttpMethod.Post, new Uri(Admin, UriKind.Relative));
        requisicao.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        requisicao.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        using HttpResponseMessage resposta = await client.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory(DisplayName = "POST e PUT sem Idempotency-Key retornam 400")]
    [InlineData("POST")]
    [InlineData("PUT")]
    public async Task Gravar_SemIdempotencyKey_Retorna400(string metodo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        string rota = metodo == "POST" ? Admin : $"{Admin}/{Guid.CreateVersion7()}";
        using HttpRequestMessage requisicao = Autenticada(new HttpMethod(metodo), rota);
        requisicao.Content = JsonContent.Create(Numerico());

        using HttpResponseMessage resposta = await client.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "POST cria o tipo (201), devolve o id e o Location da leitura pública")]
    public async Task Criar_ComAdmin_Retorna201ComLocation()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await PostarAsync(client, Numerico());

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await resposta.Content.ReadFromJsonAsync<Guid>();
        resposta.Headers.Location!.ToString().Should().EndWith($"/tipos-conceito/{id}");
        (await ObterAsync(client, id)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "POST repetido com a mesma Idempotency-Key reproduz a resposta sem criar outro tipo")]
    public async Task Criar_MesmaChave_ReproduzARespostaSemDuplicar()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        string chave = Guid.NewGuid().ToString();
        Dictionary<string, object?> corpo = Numerico();

        using HttpResponseMessage primeira = await PostarAsync(client, corpo, chave: chave);
        using HttpResponseMessage repetida = await PostarAsync(client, corpo, chave: chave);

        repetida.StatusCode.Should().Be(HttpStatusCode.Created, "a repetição não vira 409 de código já existente");
        (await repetida.Content.ReadFromJsonAsync<Guid>()).Should().Be(await primeira.Content.ReadFromJsonAsync<Guid>());
    }

    [Fact(DisplayName = "POST com código já usado retorna 409 codigo_ja_existe")]
    public async Task Criar_CodigoJaExiste_Retorna409()
    {
        string codigo = CodigoUnico();
        using HttpClient client = _fixture.Factory.CreateClient();
        await CriarAsync(client, Numerico(codigo));

        using HttpResponseMessage resposta = await PostarAsync(client, Categorico(codigo));

        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using JsonDocument problema = await LerAsync(resposta);
        problema.RootElement.GetProperty("code").GetString().Should().Be("uniplus.configuracao.tipo_conceito.codigo_ja_existe");
    }

    [Fact(DisplayName = "DELETE desativa o tipo, tira-o da leitura pública e mantém o código reservado")]
    public async Task Desativar_OcultaDaLeituraPublicaEMantemOCodigoReservado()
    {
        string codigo = CodigoUnico();
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarAsync(client, Numerico(codigo));

        using HttpResponseMessage desativar = await DesativarAsync(client, id);

        desativar.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ObterAsync(client, id)).StatusCode.Should().Be(HttpStatusCode.NotFound, "a leitura pública só expõe ativos");
        (await IdsAtivosAsync(client, limite: 50)).Should().NotContain(id);
        (await PostarAsync(client, Numerico(codigo))).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "DELETE de tipo inexistente retorna 404")]
    public async Task Desativar_Inexistente_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await DesativarAsync(client, Guid.CreateVersion7());

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "PUT altera nome e descrição e mantém o código imutável")]
    public async Task Atualizar_AlteraOsDadosDescritivos()
    {
        string codigo = CodigoUnico();
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarAsync(client, Numerico(codigo));

        using HttpResponseMessage resposta = await AtualizarAsync(
            client, id, Numerico().Sem("codigo").Com("id", id).Com("nome", "Nota renomeada").Com("descricao", "Descrição nova"));

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using JsonDocument tipo = await LerAsync(await ObterAsync(client, id));
        (tipo.RootElement.GetProperty("codigo").GetString(), tipo.RootElement.GetProperty("nome").GetString())
            .Should().Be((codigo, "Nota renomeada"));
    }

    [Fact(DisplayName = "PUT que troca a natureza para categórico substitui a escala pelos valores")]
    public async Task Atualizar_TrocaANatureza_SubstituiOsParametros()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarAsync(client, Numerico());

        using HttpResponseMessage resposta = await AtualizarAsync(client, id, Categorico().Sem("codigo").Com("id", id));

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using JsonDocument tipo = await LerAsync(await ObterAsync(client, id));
        tipo.RootElement.GetProperty("natureza").GetProperty("codigo").GetString().Should().Be("CATEGORICO");
        tipo.RootElement.GetProperty("valores").GetArrayLength().Should().Be(2);
        Ausente(tipo.RootElement, "numerico").Should().BeTrue("a escala da natureza anterior é descartada");
    }

    [Fact(DisplayName = "PUT com Id da URL diferente do Id do corpo retorna 400")]
    public async Task Atualizar_IdDivergente_Retorna400()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarAsync(client, Numerico());

        using HttpResponseMessage resposta = await AtualizarAsync(client, id, Numerico().Sem("codigo").Com("id", Guid.CreateVersion7()));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "PUT de tipo inexistente retorna 404 nao_encontrado")]
    public async Task Atualizar_Inexistente_Retorna404()
    {
        Guid id = Guid.CreateVersion7();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await AtualizarAsync(client, id, Numerico().Sem("codigo").Com("id", id));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using JsonDocument problema = await LerAsync(resposta);
        problema.RootElement.GetProperty("code").GetString().Should().Be("uniplus.configuracao.tipo_conceito.nao_encontrado");
    }
}
