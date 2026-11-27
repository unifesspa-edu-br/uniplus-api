namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Testes.Compartilhado;

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

    [Fact(DisplayName = "O descritivo e o conteúdo lidos voltam ao PUT sem transformação, e a leitura seguinte é igual")]
    public async Task LeituraVoltaAoPut_SemTransformacao()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarAsync(client, Habilitacao(CodigoUnico()));
        JsonObject antes = await ObterAsync(client, id);

        HttpResponseMessage reescrita = await EnviarAsync(
            client, HttpMethod.Put, $"{Base}/{id}", new { descritivo = antes["descritivo"]!.DeepClone(), conteudo = antes["conteudo"]!.DeepClone() });

        reescrita.StatusCode.Should().Be(HttpStatusCode.NoContent, await reescrita.Content.ReadAsStringAsync());
        JsonNode.DeepEquals(await ObterAsync(client, id), antes).Should().BeTrue();
    }

    [Fact(DisplayName = "O descritivo ausente na edição é recusado com o nome obrigatório no caminho do descritivo")]
    public async Task DescritivoAusente_RecusaONomeNoCaminhoDoDescritivo()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarAsync(client, Habilitacao(CodigoUnico()));
        JsonNode conteudo = (await ObterAsync(client, id))["conteudo"]!.DeepClone();

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Put, $"{Base}/{id}", new { conteudo });

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        string corpo = await resposta.Content.ReadAsStringAsync();
        corpo.Should().Contain("descritivo.nome").And.Contain("uniplus.configuracao.modelo_formulario.nome_obrigatorio");
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

    [Theory(DisplayName = "A declaração ausente ou incompleta no cadastro é recusada com 422 no caminho da declaração")]
    [InlineData(null)]
    [InlineData("{\"serveAFaseDeInscricao\":false}")]
    [InlineData("{\"obrigatorio\":true}")]
    public async Task Criar_DeclaracaoIncompleta_Retorna422NoCaminhoDaDeclaracao(string? declaracao)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        JsonNode? corpoDaDeclaracao = declaracao is null ? null : JsonNode.Parse(declaracao);

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Post, Base, ComDeclaracao(Habilitacao(CodigoUnico()), corpoDaDeclaracao));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("errors").EnumerateArray()
            .Select(static e => (e.GetProperty("field").GetString(), e.GetProperty("code").GetString()))
            .Should().Contain(("declaracao", "uniplus.configuracao.modelo_formulario.declaracao_incompleta"));
    }

    [Fact(DisplayName = "O modelo facultativo de outra fase é cadastrado e lido com a declaração informada")]
    public async Task Criar_FacultativoDeOutraFase_LeComADeclaracaoInformada()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        JsonNode declaracao = JsonNode.Parse("{\"serveAFaseDeInscricao\":false,\"obrigatorio\":false}")!;

        Guid id = await CriarAsync(client, ComDeclaracao(Habilitacao(CodigoUnico()), declaracao));

        JsonNode lida = (await ObterAsync(client, id))["declaracao"]!;
        (lida["serveAFaseDeInscricao"]!.GetValue<bool>(), lida["obrigatorio"]!.GetValue<bool>()).Should().Be((false, false));
    }

    [Fact(DisplayName = "O filtro de servir à fase de inscrição que não é verdadeiro nem falso é recusado com 400")]
    public async Task Listar_FiltroDeInscricaoInvalido_Retorna400()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        (await StatusAsync(client, HttpMethod.Get, $"{Base}?serveAFaseDeInscricao=talvez")).Should().Be(HttpStatusCode.BadRequest);
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
        depois["descritivo"]!["nome"]!.GetValue<string>().Should().Be("Habilitação Medicina 2027");
        depois["conteudo"]!["termos"]!.AsArray().Should().BeEmpty();
    }

    [Fact(DisplayName = "O cadastro com item que a leitura recusa e item de forma inválida devolve as duas recusas em errors[], sem gravar o modelo")]
    public async Task Criar_RecusaDeLeituraEDeForma_DevolveAsDuasEmErrors()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        JsonNode corpo = ComItemNaoLidoEItemSemRotulo(JsonSerializer.SerializeToNode(Habilitacao(CodigoUnico()))!);

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Post, Base, corpo);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ErrosAsync(resposta)).Should().Contain(
        [
            ("conteudo.itens[0].regras.obrigatoriedade", "uniplus.configuracao.modelo_formulario.obrigatoriedade_invalida"),
            ("conteudo.itens[1].apresentacao.rotulo", "uniplus.item_formulario.rotulo_obrigatorio"),
        ]);
    }

    [Fact(DisplayName = "O cadastro com grupo sem mínimo e com máximo zero devolve as duas recusas do grupo em errors[], o mínimo antes do máximo")]
    public async Task Criar_GrupoSemMinimoEComMaximoZero_DevolveAsDuasRecusasDoGrupo()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        JsonNode corpo = JsonSerializer.SerializeToNode(Habilitacao(CodigoUnico()))!;
        GrupoColetadoInput grupo = EntradaDoGrupoColetadoDeTeste.Compondo("COMPOSICAO", 1).ComRotulo("Composição").ComObrigatoriedade("SEMPRE")
            .ComCampos(EntradaDoFatoColetadoDeTeste.Coletando("MAIOR_IDADE").ComRotulo("Maior de idade").DoTipo("SIM_OU_NAO").Construir()).Construir()
            with { Ocorrencias = new OcorrenciasDoGrupoInput(null, false, 0) };
        corpo["conteudo"]!["grupos"] = new JsonArray(JsonSerializer.SerializeToNode(grupo));

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Post, Base, corpo);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ErrosAsync(resposta)).Where(static e => e.Item1!.StartsWith("conteudo.grupos[0]", StringComparison.Ordinal)).Should().Equal(
            ("conteudo.grupos[0].ocorrencias.minimo", "uniplus.grupo_formulario.ocorrencias_obrigatorias"),
            ("conteudo.grupos[0].ocorrencias.maximo", "uniplus.grupo_formulario.contagem_incoerente"));
    }

    [Fact(DisplayName = "A edição com item que a leitura recusa e item de forma inválida devolve as duas recusas em errors[], e a leitura seguinte mostra o modelo inalterado")]
    public async Task Atualizar_RecusaDeLeituraEDeForma_DevolveAsDuasEMantemOModelo()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarAsync(client, Habilitacao(CodigoUnico()));
        JsonObject antes = await ObterAsync(client, id);
        JsonNode conteudo = ComItemNaoLidoEItemSemRotulo(new JsonObject { ["conteudo"] = antes["conteudo"]!.DeepClone() })["conteudo"]!;

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Put, $"{Base}/{id}", Edicao("Nome que não deve ficar", conteudo));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ErrosAsync(resposta)).Should().Contain(
        [
            ("conteudo.itens[0].regras.obrigatoriedade", "uniplus.configuracao.modelo_formulario.obrigatoriedade_invalida"),
            ("conteudo.itens[1].apresentacao.rotulo", "uniplus.item_formulario.rotulo_obrigatorio"),
        ]);
        JsonNode.DeepEquals(await ObterAsync(client, id), antes).Should().BeTrue();
    }

    [Fact(DisplayName = "Tipo de processo com caractere nulo é recusado pelo modelo, sem chegar ao banco; na lista, a página é vazia")]
    public async Task TipoProcessoComCaractereNulo_RecusaSemErroDoBanco()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        object modelo = ComTipoDeProcesso(Habilitacao(CodigoUnico()), "REGULAR_UNIFICADO\u0000");

        HttpResponseMessage criar = await EnviarAsync(client, HttpMethod.Post, Base, modelo);
        HttpResponseMessage listar = await EnviarAsync(client, HttpMethod.Get, $"{Base}?tipoProcesso=%00");

        criar.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        await ConferirCodigoDoErroAsync(criar, "uniplus.configuracao.modelo_formulario.tipo_processo_codigo_formato_invalido");
        listar.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument lista = JsonDocument.Parse(await listar.Content.ReadAsStringAsync());
        lista.RootElement.GetArrayLength().Should().Be(0);
    }

    [Fact(DisplayName = "A pré-visualização avalia o modelo com as respostas simuladas")]
    public async Task PreVisualizar_RespostasSimuladas_AvaliaOModelo()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarAsync(client, Habilitacao(CodigoUnico()));
        Dictionary<string, object> respostas = new() { ["QUILOMBOLA"] = false };

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Post, $"{Base}/{id}/pre-visualizacao", new { respostas });

        JsonObject resultado = await LerAsync(resposta);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.uniplus.pre-visualizacao-modelo-formulario.v1+json");
        Item(resultado["itens"]!.AsArray(), "BAIXA_RENDA")["visivel"]!.GetValue<string>().Should().Be("FALSO");
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
        lido["declaracao"]!["serveAFaseDeInscricao"]!.GetValue<bool>().Should().BeFalse();
        lido["declaracao"]!["obrigatorio"]!.GetValue<bool>().Should().BeTrue();
        JsonNode condicao = lido["conteudo"]!["itens"]![1]!["regras"]!["exibicao"]![0]![0]!;
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
        using (JsonDocument inativos = await ListarAsync(client, "?ativo=false&serveAFaseDeInscricao=false&tipoProcesso=QUALQUER_TIPO"))
        {
            inativos.RootElement.EnumerateArray().Should().Contain(m => m.GetProperty("id").GetGuid() == id
                && m.GetProperty("descritivo").GetProperty("nome").GetString() == "Habilitação editada");
        }

        using JsonDocument daInscricao = await ListarAsync(client, "?serveAFaseDeInscricao=true");
        daInscricao.RootElement.EnumerateArray().Should().NotContain(m => m.GetProperty("id").GetGuid() == id);
    }

    private static void ConferirSecaoDosDadosBasicos(JsonObject lido)
    {
        lido["conteudo"]!["paginas"]![0]!["codigo"]!.GetValue<string>().Should().Be("DADOS_BASICOS");
        JsonArray itens = lido["conteudo"]!["itens"]!.AsArray();
        itens.Where(static i => i!["paginaCodigo"]!.GetValue<string>() == "DADOS_BASICOS").Should().HaveCount(ConjuntoBasicoDaInscricao.Itens.Count);
    }

    private static JsonNode ComCorRacaDispensada(JsonNode conteudo)
    {
        JsonNode alterado = conteudo.DeepClone();
        Item(alterado["itens"]!.AsArray(), "COR_RACA")["regras"]!["obrigatoriedade"]!["tipo"] = "NUNCA";
        return alterado;
    }

    private static JsonArray TermosDeCadastroInexistente() => new(new JsonObject
    {
        ["codigo"] = "LGPD",
        ["ordem"] = 0,
        ["termo"] = new JsonObject { ["termoId"] = Guid.NewGuid(), ["versaoId"] = Guid.NewGuid() },
        ["regras"] = new JsonObject { ["exibicao"] = null, ["obrigatoriedade"] = new JsonObject { ["tipo"] = "SEMPRE" } },
    });

    private static JsonNode ComItemNaoLidoEItemSemRotulo(JsonNode corpo)
    {
        JsonArray itens = corpo["conteudo"]!["itens"]!.AsArray();
        itens[0]!["regras"]!["obrigatoriedade"]!["tipo"] = "INEXISTENTE";
        itens[1]!["apresentacao"]!["rotulo"] = string.Empty;
        return corpo;
    }

    private static async Task<(string?, string?)[]> ErrosAsync(HttpResponseMessage resposta)
    {
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return [.. problema.RootElement.GetProperty("errors").EnumerateArray()
            .Select(static e => (e.GetProperty("field").GetString(), e.GetProperty("code").GetString()))];
    }

    private static async Task ConferirCodigoDoErroAsync(HttpResponseMessage resposta, string codigo)
    {
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("errors").EnumerateArray()
            .Select(static e => e.GetProperty("code").GetString()).Should().Contain(codigo);
    }
}
