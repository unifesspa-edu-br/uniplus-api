namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposConceito;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure.TiposConceitoHttpDeTeste;

/// <summary>
/// A manutenção do catálogo de tipos de conceito pertence a quem tem a permissão de manter: o papel de administrador
/// da plataforma no token não basta, e a recusa vem antes de a API procurar o tipo ou validar o corpo.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class TipoConceitoPermissaoEndpointTests : IAsyncLifetime
{
    private const string PessoaSemConcessao = "diogo.souza";

    private readonly ConfiguracaoEndpointFixture _fixture;

    public TipoConceitoPermissaoEndpointTests(ConfiguracaoEndpointFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, PessoaSemConcessao);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "POST sem a permissão de manter retorna 403 mesmo com o papel de administrador, e nada é criado")]
    public async Task Criar_SemPermissao_Retorna403ENaoCria()
    {
        string codigo = CodigoUnico();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage recusada = await PostarAsync(client, Numerico(codigo), PessoaSemConcessao);
        using HttpResponseMessage criada = await PostarAsync(client, Numerico(codigo));

        recusada.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        criada.StatusCode.Should().Be(HttpStatusCode.Created, "a recusa não gravou o código");
    }

    [Fact(DisplayName = "PUT sem a permissão de manter retorna 403 antes de procurar o tipo, que inexistente seria 404")]
    public async Task Atualizar_SemPermissao_Retorna403AntesDeProcurarOTipo()
    {
        Guid id = Guid.CreateVersion7();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await AtualizarAsync(
            client, id, Numerico().Sem("codigo").Com("id", id), PessoaSemConcessao);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "PUT sem a permissão de manter retorna 403 antes de validar o corpo, que inválido seria 422")]
    public async Task Atualizar_SemPermissao_Retorna403AntesDeValidarOCorpo()
    {
        Guid id = Guid.CreateVersion7();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await AtualizarAsync(
            client, id, Numerico().Sem("codigo").Com("id", id).Com("nome", ""), PessoaSemConcessao);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "DELETE sem a permissão de manter retorna 403 e o tipo continua ativo")]
    public async Task Desativar_SemPermissao_Retorna403EMantemOTipo()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarAsync(client, Numerico());

        using HttpResponseMessage resposta = await DesativarAsync(client, id, PessoaSemConcessao);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ObterAsync(client, id)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "A negativa por falta de permissão não ecoa o corpo enviado na resposta")]
    public async Task Criar_SemPermissao_NaoEcoaOCorpo()
    {
        string codigo = CodigoUnico();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await PostarAsync(client, Numerico(codigo), PessoaSemConcessao);

        (await resposta.Content.ReadAsStringAsync()).Should().NotContain(codigo);
    }

    [Fact(DisplayName = "A leitura pública não exige permissão")]
    public async Task Listar_SemConcessao_Retorna200()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        using HttpResponseMessage resposta = await client.GetAsync(new Uri(Publico, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument lista = await LerAsync(resposta);
        lista.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }
}
