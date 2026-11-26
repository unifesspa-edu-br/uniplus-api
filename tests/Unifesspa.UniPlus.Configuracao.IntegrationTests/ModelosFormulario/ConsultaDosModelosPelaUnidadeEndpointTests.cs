namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario.ModeloFormularioHttp;
using static Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario.ModelosParaTeste;

/// <summary>
/// Quem tem a permissão de consultar os modelos de formulário numa unidade consulta os modelos para escolher e
/// aplicar o de seu processo, mas não os altera: as cinco consultas passam com a concessão numa unidade, as quatro
/// alterações seguem exigindo a permissão de manter, e sem concessão a recusa vem antes de ler o cadastro.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class ConsultaDosModelosPelaUnidadeEndpointTests : IAsyncLifetime
{
    private const string Leandro = "leandro.viana";
    private const string Avaliacoes = "/api/configuracao/admin/avaliacoes-de-formulario";

    private const string AcompanharProcessos = "selecao:processos:acompanhar";

    private const string Listagem = "listagem";
    private const string Obtencao = "obtenção";
    private const string Apresentacao = "apresentação";
    private const string AvaliacaoSemCadastro = "avaliação sem cadastro";
    private const string PreVisualizacao = "pré-visualização";

    private static readonly string ConsultarModelosNaUnidade = PermissaoConfiguracao.ModelosFormularioConsultarNaUnidade.Codigo;

    private readonly ConfiguracaoEndpointFixture _fixture;

    public ConsultaDosModelosPelaUnidadeEndpointTests(ConfiguracaoEndpointFixture fixture) => _fixture = fixture;

    public static TheoryData<string> Consultas =>
        [Listagem, Obtencao, Apresentacao, AvaliacaoSemCadastro, PreVisualizacao];

    public Task InitializeAsync() => ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Leandro);

    public Task DisposeAsync() => ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Leandro);

    [Theory(DisplayName = "A concessão de consultar na unidade abre cada uma das cinco consultas dos modelos")]
    [MemberData(nameof(Consultas))]
    public async Task Consulta_ComConcessaoNaUnidade_Passa(string consulta)
    {
        Alvo alvo = await CriarAlvoAsync();
        await ConcederNaUnidadeAsync(ConsultarModelosNaUnidade);

        using HttpResponseMessage resposta = await EnviarComoLeandroAsync(PedidoDa(consulta, alvo));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, $"{consulta}: {await resposta.Content.ReadAsStringAsync()}");
    }

    [Theory(DisplayName = "Sem concessão, cada consulta dos modelos responde 403 antes de ler o cadastro")]
    [MemberData(nameof(Consultas))]
    public async Task Consulta_SemConcessao_403AntesDeLerOCadastro(string consulta)
    {
        Alvo inexistente = new(Guid.NewGuid(), null);

        using HttpResponseMessage resposta = await EnviarComoLeandroAsync(PedidoDa(consulta, inexistente));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden, "um modelo que não existe só responderia 404 se o cadastro fosse lido antes da recusa");
    }

    [Theory(DisplayName = "Outra permissão numa unidade não abre as consultas dos modelos")]
    [MemberData(nameof(Consultas))]
    public async Task Consulta_ComOutraPermissaoNaUnidade_403(string consulta)
    {
        Alvo inexistente = new(Guid.NewGuid(), null);
        await ConcederNaUnidadeAsync(AcompanharProcessos);

        using HttpResponseMessage resposta = await EnviarComoLeandroAsync(PedidoDa(consulta, inexistente));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden, consulta);
    }

    [Fact(DisplayName = "Consultar na unidade não cadastra modelo: 403, e o código do modelo segue livre")]
    public async Task Cadastro_ComConsultaNaUnidade_403SemGravar()
    {
        await ConcederNaUnidadeAsync(ConsultarModelosNaUnidade);
        string codigo = CodigoUnico();

        using HttpResponseMessage recusada = await EnviarComoLeandroAsync(new Pedido(HttpMethod.Post, Base, Habilitacao(codigo)));

        recusada.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using HttpClient administrador = _fixture.Factory.CreateClient();
        await CriarAsync(administrador, Habilitacao(codigo)); // seria recusado por código repetido se o cadastro recusado tivesse gravado
    }

    [Fact(DisplayName = "Consultar na unidade não edita modelo: 403, e o modelo segue como estava")]
    public async Task Edicao_ComConsultaNaUnidade_403SemGravar()
    {
        using HttpClient administrador = _fixture.Factory.CreateClient();
        Guid id = await CriarAsync(administrador, Habilitacao(CodigoUnico()));
        JsonObject antes = await ObterAsync(administrador, id);
        await ConcederNaUnidadeAsync(ConsultarModelosNaUnidade);

        using HttpResponseMessage recusada = await EnviarComoLeandroAsync(
            new Pedido(HttpMethod.Put, $"{Base}/{id}", Edicao("Nome que não deve ficar", antes["conteudo"])));

        recusada.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        JsonNode.DeepEquals(await ObterAsync(administrador, id), antes).Should().BeTrue();
    }

    [Fact(DisplayName = "Consultar na unidade não ativa modelo: 403, e o modelo segue desativado")]
    public async Task Ativacao_ComConsultaNaUnidade_403SemGravar()
    {
        using HttpClient administrador = _fixture.Factory.CreateClient();
        Guid id = await CriarAsync(administrador, Habilitacao(CodigoUnico()));
        await ConcederNaUnidadeAsync(ConsultarModelosNaUnidade);

        using HttpResponseMessage recusada = await EnviarComoLeandroAsync(new Pedido(HttpMethod.Post, $"{Base}/{id}/ativacao"));

        recusada.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ObterAsync(administrador, id))["ativo"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact(DisplayName = "Consultar na unidade não desativa modelo: 403, e o modelo segue ativo")]
    public async Task Desativacao_ComConsultaNaUnidade_403SemGravar()
    {
        using HttpClient administrador = _fixture.Factory.CreateClient();
        Guid id = await CriarAsync(administrador, Habilitacao(CodigoUnico()));
        (await StatusAsync(administrador, HttpMethod.Post, $"{Base}/{id}/ativacao")).Should().Be(HttpStatusCode.NoContent);
        await ConcederNaUnidadeAsync(ConsultarModelosNaUnidade);

        using HttpResponseMessage recusada = await EnviarComoLeandroAsync(new Pedido(HttpMethod.Delete, $"{Base}/{id}"));

        recusada.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ObterAsync(administrador, id))["ativo"]!.GetValue<bool>().Should().BeTrue();
    }

    private static Pedido PedidoDa(string consulta, Alvo alvo) => consulta switch
    {
        Listagem => new Pedido(HttpMethod.Get, Base),
        Obtencao => new Pedido(HttpMethod.Get, $"{Base}/{alvo.Id}"),
        Apresentacao => new Pedido(HttpMethod.Get, $"{Base}/{alvo.Id}/apresentacao"),
        AvaliacaoSemCadastro => new Pedido(HttpMethod.Post, Avaliacoes, new { regras = alvo.Regras, respostas = Respostas() }),
        PreVisualizacao => new Pedido(HttpMethod.Post, $"{Base}/{alvo.Id}/pre-visualizacao", new { respostas = Respostas() }),
        _ => throw new ArgumentOutOfRangeException(nameof(consulta), consulta, "Consulta desconhecida."),
    };

    private static Dictionary<string, object> Respostas() => new() { ["QUILOMBOLA"] = false };

    private async Task<Alvo> CriarAlvoAsync()
    {
        using HttpClient administrador = _fixture.Factory.CreateClient();
        Guid id = await CriarAsync(administrador, Habilitacao(CodigoUnico()));
        return new Alvo(id, (await ApresentacaoAsync(administrador, id))["regras"]);
    }

    private async Task ConcederNaUnidadeAsync(string permissao)
    {
        IServiceProvider servicos = _fixture.Factory.Services;
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(servicos, permissao);
        await ConcessoesDeTeste.ConcederAsync(servicos, Leandro, perfil, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));
    }

    private async Task<HttpResponseMessage> EnviarComoLeandroAsync(Pedido pedido)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(pedido.Metodo, new Uri(pedido.Url, UriKind.Relative))
        {
            Content = pedido.Corpo is null ? null : JsonContent.Create(pedido.Corpo),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, Leandro);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }

    private sealed record Pedido(HttpMethod Metodo, string Url, object? Corpo = null);

    private sealed record Alvo(Guid Id, JsonNode? Regras);
}
