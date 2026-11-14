namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Regras.Formularios;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario.ModeloFormularioHttp;
using static Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario.ModelosParaTeste;

/// <summary>
/// Contrato HTTP da manutenção dos modelos de formulário pela permissão de manter
/// (UNI-REQ-0144): cadastro conferido contra o catálogo, leitura no formato da escrita, edição,
/// desativação e reativação.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class ModeloFormularioAdminEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public ModeloFormularioAdminEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Cadastro desativado, ativação, leitura no formato da escrita, regravação do lido, desativação e reativação; a lista filtra")]
    public async Task CicloDoModelo()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarAsync(client, Habilitacao(CodigoUnico()));
        (await ObterAsync(client, id))["ativo"]!.GetValue<bool>().Should().BeFalse();
        await AtivarAsync(client, id);
        (await StatusAsync(client, HttpMethod.Post, $"{Base}/{id}/ativacao")).Should().Be(HttpStatusCode.UnprocessableEntity);
        JsonObject lido = await ObterAsync(client, id);
        ConferirHabilitacaoAtivaLida(lido);
        await RegravarLidoEDesativarAsync(client, id, lido);
        await ConferirListasAsync(client, id);
        await AtivarAsync(client, id);
    }

    [Fact(DisplayName = "O modelo de inscrição ganha a seção dos dados básicos, cita um deles sem declará-lo e recusa alterá-lo")]
    public async Task ModeloDeInscricao_SecaoDosDadosBasicos()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarAsync(client, Inscricao(CodigoUnico()));
        JsonObject lido = await ObterAsync(client, id);
        ConferirSecaoDosDadosBasicos(lido);

        // O conteúdo lido volta à escrita sem conversão; o dado básico alterado é recusado.
        (await StatusAsync(client, HttpMethod.Put, $"{Base}/{id}", Edicao("Inscrição", lido["conteudo"]))).Should().Be(HttpStatusCode.NoContent);
        HttpResponseMessage recusada = await EnviarAsync(client, HttpMethod.Put, $"{Base}/{id}", Edicao("Inscrição", ComCorRacaDispensada(lido["conteudo"]!)));

        recusada.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await recusada.Content.ReadAsStringAsync()).Should().Contain("uniplus.estrutura_formulario.dado_basico_alterado");
    }

    [Fact(DisplayName = "Item de fato derivado é recusado pela conferência contra o catálogo")]
    public async Task Criar_ItemDeFatoDerivado_Retorna422()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Post, Base, Habilitacao(CodigoUnico(), fatoDoPrimeiroItem: "MODALIDADE"));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("errors").EnumerateArray()
            .Select(static e => (e.GetProperty("field").GetString(), e.GetProperty("code").GetString()))
            .Should().Contain(("conteudo.itens[0].fatoCodigo", "uniplus.item_formulario.fato_nao_coletavel"));
    }

    [Fact(DisplayName = "Edição recusada pelo catálogo não grava nada do que trazia")]
    public async Task Atualizar_RecusadaPeloCatalogo_NaoGrava()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarAsync(client, Habilitacao(CodigoUnico()));
        JsonNode conteudo = (await ObterAsync(client, id))["conteudo"]!.DeepClone();
        conteudo["termos"] = TermosDeCadastroInexistente();

        (await StatusAsync(client, HttpMethod.Put, $"{Base}/{id}", Edicao("Nome que não deve ficar", conteudo))).Should().Be(HttpStatusCode.UnprocessableEntity);

        JsonObject depois = await ObterAsync(client, id);
        depois["nome"]!.GetValue<string>().Should().Be("Habilitação Medicina 2027");
        depois["conteudo"]!["termos"]!.AsArray().Should().BeEmpty();
    }

    [Fact(DisplayName = "Tipo de processo com caractere nulo é recusado pelo modelo, sem chegar ao banco; na lista, a página é vazia")]
    public async Task TipoProcessoComCaractereNulo_RecusaSemErroDoBanco()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        object modelo = ComTipoDeProcesso(Habilitacao(CodigoUnico()), "PSR\u0000");

        HttpResponseMessage criar = await EnviarAsync(client, HttpMethod.Post, Base, modelo);
        HttpResponseMessage listar = await EnviarAsync(client, HttpMethod.Get, $"{Base}?tipoProcesso=%00");

        criar.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        await ConferirCodigoDoErroAsync(criar, "uniplus.configuracao.modelo_formulario.texto_nao_gravavel");
        listar.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument lista = JsonDocument.Parse(await listar.Content.ReadAsStringAsync());
        lista.RootElement.GetArrayLength().Should().Be(0);
    }

    [Fact(DisplayName = "A pré-visualização avalia o modelo com as respostas simuladas; modelo inexistente é 404")]
    public async Task PreVisualizar_RespostasSimuladas_AvaliaOModelo()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarAsync(client, Habilitacao(CodigoUnico()));
        Dictionary<string, object> respostas = new() { ["QUILOMBOLA"] = false };

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Post, $"{Base}/{id}/pre-visualizacao", new { respostas });

        JsonObject resultado = await LerAsync(resposta);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.uniplus.pre-visualizacao-modelo-formulario.v1+json");
        Item(resultado["itens"]!.AsArray(), "BAIXA_RENDA")["visivel"]!.GetValue<string>().Should().Be("FALSO");
        (await StatusAsync(client, HttpMethod.Post, $"{Base}/{Guid.NewGuid()}/pre-visualizacao", new { respostas })).Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "Código já usado por outro modelo é recusado com conflito")]
    public async Task Criar_CodigoExistente_Retorna409()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        string codigo = CodigoUnico();
        (await StatusAsync(client, HttpMethod.Post, Base, Habilitacao(codigo))).Should().Be(HttpStatusCode.Created);

        (await StatusAsync(client, HttpMethod.Post, Base, Habilitacao(codigo))).Should().Be(HttpStatusCode.Conflict);
    }

    private static async Task AtivarAsync(HttpClient client, Guid id) =>
        (await StatusAsync(client, HttpMethod.Post, $"{Base}/{id}/ativacao")).Should().Be(HttpStatusCode.NoContent);

    private static void ConferirHabilitacaoAtivaLida(JsonObject lido)
    {
        lido["ativo"]!.GetValue<bool>().Should().BeTrue();
        lido["finalidade"]!.GetValue<string>().Should().Be("HABILITACAO");
        JsonNode condicao = lido["conteudo"]!["itens"]![1]!["precondicao"]![0]![0]!;
        condicao["fato"]!.GetValue<string>().Should().Be("QUILOMBOLA");
        condicao["valor"]!.GetValue<bool>().Should().BeTrue();
    }

    private static async Task RegravarLidoEDesativarAsync(HttpClient client, Guid id, JsonObject lido)
    {
        // O conteúdo lido volta à escrita sem conversão.
        (await StatusAsync(client, HttpMethod.Put, $"{Base}/{id}", Edicao("Habilitação editada", lido["conteudo"]))).Should().Be(HttpStatusCode.NoContent);
        (await StatusAsync(client, HttpMethod.Delete, $"{Base}/{id}")).Should().Be(HttpStatusCode.NoContent);
    }

    private static async Task ConferirListasAsync(HttpClient client, Guid id)
    {
        using (JsonDocument inativos = await ListarAsync(client, "?ativo=false&finalidade=HABILITACAO&tipoProcesso=QUALQUER_TIPO"))
        {
            inativos.RootElement.EnumerateArray().Should().Contain(m => m.GetProperty("id").GetGuid() == id
                && m.GetProperty("nome").GetString() == "Habilitação editada");
        }

        using JsonDocument outraFinalidade = await ListarAsync(client, "?finalidade=INSCRICAO");
        outraFinalidade.RootElement.EnumerateArray().Should().NotContain(m => m.GetProperty("id").GetGuid() == id);
    }

    private static void ConferirSecaoDosDadosBasicos(JsonObject lido)
    {
        lido["conteudo"]!["etapas"]![0]!["codigo"]!.GetValue<string>().Should().Be("DADOS_BASICOS");
        JsonArray itens = lido["conteudo"]!["itens"]!.AsArray();
        itens.Where(static i => i!["etapaCodigo"]!.GetValue<string>() == "DADOS_BASICOS").Should().HaveCount(ConjuntoBasicoDaInscricao.Itens.Count);
    }

    private static JsonNode ComCorRacaDispensada(JsonNode conteudo)
    {
        JsonNode alterado = conteudo.DeepClone();
        Item(alterado["itens"]!.AsArray(), "COR_RACA")["obrigatoriedade"] = "NUNCA";
        return alterado;
    }

    private static JsonArray TermosDeCadastroInexistente() => new(new JsonObject
    {
        ["codigo"] = "LGPD",
        ["ordem"] = 0,
        ["termoId"] = Guid.NewGuid(),
        ["versaoId"] = Guid.NewGuid(),
        ["exibicao"] = null,
        ["obrigatoriedade"] = "SEMPRE",
        ["predicadoObrigatoriedade"] = null,
    });

    private static async Task ConferirCodigoDoErroAsync(HttpResponseMessage resposta, string codigo)
    {
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("errors").EnumerateArray()
            .Select(static e => e.GetProperty("code").GetString()).Should().Contain(codigo);
    }
}
