namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario.ModeloFormularioHttp;

/// <summary>
/// A simulação sem cadastro devolve todas as recusas do formulário recebido, cada uma no campo do seu elemento, e
/// não repete na resposta o código enviado.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class RecusasDaSimulacaoSemCadastroEndpointTests
{
    private const string Avaliacoes = "/api/configuracao/admin/avaliacoes-de-formulario";
    private const string CodigoFicticio = "CODIGO_FICTICIO_DE_TESTE";
    private const string ObrigatoriedadeInvalida = "uniplus.formulario_portavel.obrigatoriedade_invalida";
    private const string EstruturaInvalida = "uniplus.formulario_portavel.estrutura_invalida";

    private readonly ConfiguracaoEndpointFixture _fixture;

    public RecusasDaSimulacaoSemCadastroEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Dois itens inválidos em páginas diferentes voltam como duas recusas, cada uma no campo do seu item, na ordem das páginas")]
    public async Task Simulacao_ItensInvalidosEmPaginasDiferentes_DevolveUmaRecusaPorItem()
    {
        object regras = Formulario(
            Pagina("DADOS", ItemValido("PRIMEIRO"), ItemInvalido(CodigoFicticio)),
            Pagina("INTERMEDIARIA", ItemValido("SEGUNDO")),
            Pagina("FINAL", ItemInvalido("TERCEIRO")));

        using JsonDocument problema = await RecusarAsync(regras);

        Recusas(problema).Should().Equal(
            ("paginas[0].itens[1].regras.obrigatoriedade", ObrigatoriedadeInvalida),
            ("paginas[2].itens[0].regras.obrigatoriedade", ObrigatoriedadeInvalida));
    }

    [Fact(DisplayName = "Elemento nulo e item inválido em outra página devolvem só a recusa de forma, com o caminho no campo")]
    public async Task Simulacao_ElementoNuloEItemInvalidoEmOutraPagina_DevolveSoARecusaDeForma()
    {
        object regras = Formulario(
            Pagina("DADOS", ItemValido("PRIMEIRO"), null),
            Pagina("FINAL", ItemInvalido("TERCEIRO")));

        using JsonDocument problema = await RecusarAsync(regras);

        Recusas(problema).Should().Equal(("paginas[0].itens[1]", EstruturaInvalida));
        problema.RootElement.GetProperty("errors")[0].GetProperty("message").GetString().Should().Be("O elemento das regras é nulo.");
    }

    [Fact(DisplayName = "O código enviado não aparece no detalhe nem nas recusas")]
    public async Task Simulacao_ItemInvalido_NaoRepeteOCodigoEnviado()
    {
        object regras = Formulario(Pagina("DADOS", ItemInvalido(CodigoFicticio)));

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Post, Avaliacoes, new { regras, respostas = new Dictionary<string, object>() });

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, corpo);
        using JsonDocument problema = JsonDocument.Parse(corpo);
        problema.RootElement.GetProperty("detail").GetString().Should().NotContain(CodigoFicticio);
        problema.RootElement.GetProperty("errors").GetRawText().Should().NotContain(CodigoFicticio);
    }

    [Fact(DisplayName = "Regras sem as quatro listas voltam como 422 e uma recusa de obrigatório por lista, na ordem do contrato")]
    public async Task Simulacao_RegrasSemAsListas_DevolveUmaRecusaPorLista()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Post, Avaliacoes, new { regras = new { }, respostas = new Dictionary<string, object>() });

        await RecusaDeCampoObrigatorioNoDominio.ConferirCamposAsync(resposta, "paginas", "termos", "derivacoes", "agregados");
    }

    private async Task<JsonDocument> RecusarAsync(object regras)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Post, Avaliacoes, new { regras, respostas = new Dictionary<string, object>() });

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, corpo);
        return JsonDocument.Parse(corpo);
    }

    private static List<(string? Campo, string? Codigo)> Recusas(JsonDocument problema) =>
        [.. problema.RootElement.GetProperty("errors").EnumerateArray()
            .Select(static e => (e.GetProperty("field").GetString(), e.GetProperty("code").GetString()))];

    private static object Formulario(params object[] paginas) => new
    {
        paginas,
        termos = Array.Empty<object>(),
        derivacoes = Array.Empty<object>(),
        agregados = Array.Empty<object>(),
    };

    private static object Pagina(string codigo, params object?[] itens) =>
        new { codigo, exibicao = (object?)null, itens, grupos = Array.Empty<object>() };

    private static object ItemValido(string fato) => Item(fato, "SEMPRE");

    private static object ItemInvalido(string fato) => Item(fato, "QUANDO");

    private static object Item(string fato, string obrigatoriedade) => new
    {
        fatoCodigo = fato,
        regras = new
        {
            exibicao = (object?)null,
            obrigatoriedade = new { tipo = obrigatoriedade, predicado = (object?)null },
            restricoes = Array.Empty<object>(),
            impedimento = (object?)null,
        },
        oferta = (object?)null,
        formato = (object?)null,
    };
}
