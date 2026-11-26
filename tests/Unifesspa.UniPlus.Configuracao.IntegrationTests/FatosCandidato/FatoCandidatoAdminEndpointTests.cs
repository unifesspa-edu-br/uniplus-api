namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FatosCandidato;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;

/// <summary>
/// Contrato HTTP da manutenção do catálogo de fatos pela permissão de manter
/// (UNI-REQ-0143, ADR-0136): cadastro de fato declarado, edição de nome e descrição e desativação.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class FatoCandidatoAdminEndpointTests(ConfiguracaoEndpointFixture fixture) : FatoCandidatoAdminEndpointTestBase(fixture)
{
    private static readonly Guid IdDaRendaPerCapita = Guid.Parse("fa700000-0000-7000-8000-000000000005");

    [Fact(DisplayName = "O fato declarado cadastrado sai com a origem declarada, o vínculo ao campo e sem ser de sistema")]
    public async Task Cadastro_DevolveOFatoDeclarado()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        string codigo = CodigoUnico();
        Guid id = await CadastrarAsync(client, FatoBooleano(codigo));

        using JsonDocument fato = await ObterAsync(client, id);
        Em(fato, "definicao", "resolucao", "origem", "codigo").GetString().Should().Be("DECLARADO");
        Em(fato, "definicao", "resolucao", "binding").GetString().Should().Be($"CAMPO_FORMULARIO:{codigo}");
        Em(fato, "semeadoPeloSistema").GetBoolean().Should().BeFalse();
    }

    [Fact(DisplayName = "Edição, desativação e reativação do fato declarado; a lista filtra por ativo")]
    public async Task CicloDoFatoDeclarado()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await CadastrarAsync(client, FatoBooleano(CodigoUnico()));
        await ConferirAsync(client, HttpMethod.Put, $"{Base}/{id}", Descritivo("Nome editado", "Descrição editada.", "MOEDA"), HttpStatusCode.NoContent);
        await ConferirAsync(client, HttpMethod.Delete, $"{Base}/{id}", corpo: null, HttpStatusCode.NoContent);

        using JsonDocument lista = await ListarAsync(client, "?ativo=false&origem=DECLARADO");

        ConferirInativosDeclarados(lista, id);
        await ConferirAsync(client, HttpMethod.Post, $"{Base}/{id}/ativacao", corpo: null, HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "Cadastro sem descrição é recusado com o código da descrição obrigatória")]
    public async Task Criar_SemDescricao_Retorna422()
    {
        using HttpClient client = Fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Post, Base, FatoSemDescricao());

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("errors").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("code").GetString().Should().Be("uniplus.configuracao.fato_candidato.descricao_obrigatoria");
    }

    [Fact(DisplayName = "Componente fora do vocabulário é recusado com o código do componente de exibição")]
    public async Task Atualizar_ComponenteForaDoVocabulario_Retorna422()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await CadastrarAsync(client, FatoBooleano(CodigoUnico()));

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Put, $"{Base}/{id}", Descritivo("Nome editado", "Descrição editada.", "REAIS"));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("errors").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("code").GetString().Should().Be("uniplus.configuracao.fato_candidato.componente_exibicao_invalido");
    }

    [Fact(DisplayName = "A semente declara a renda per capita como moeda")]
    public async Task FatoDeSistema_SementeDeclaraAMoeda()
    {
        using HttpClient client = Fixture.Factory.CreateClient();

        ApresentacaoLida semente = await LerApresentacaoAsync(client, IdDaRendaPerCapita);

        semente.Componente.Should().Be("MOEDA");
        semente.Descricao.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "O fato de sistema aceita editar a apresentação, sem mudar a situação da aprovação da proteção")]
    public async Task FatoDeSistema_ApresentacaoEditavel()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        ApresentacaoLida antes = await LerApresentacaoAsync(client, IdDaRendaPerCapita);
        await ConferirAsync(client, HttpMethod.Put, $"{Base}/{IdDaRendaPerCapita}", Descritivo("Renda familiar per capita", "Descrição editada.", "SITUACAO"), HttpStatusCode.NoContent);
        try
        {
            (await LerApresentacaoAsync(client, IdDaRendaPerCapita)).Should().Be(new ApresentacaoLida("SITUACAO", "Descrição editada.", antes.Situacao));
        }
        finally
        {
            await ConferirAsync(client, HttpMethod.Put, $"{Base}/{IdDaRendaPerCapita}", Descritivo("Renda familiar per capita", antes.Descricao, "MOEDA"), HttpStatusCode.NoContent);
        }
    }

    [Fact(DisplayName = "A consulta do fato para manutenção devolve os operadores admitidos ao lado do fato")]
    public async Task Obter_DevolveOsOperadoresAdmitidosAoLadoDoFato()
    {
        using HttpClient client = Fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Get, $"{Base}/{IdDeCorRaca}", corpo: null);

        using JsonDocument envelope = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        envelope.RootElement.GetProperty("fato").GetProperty("codigo").GetString().Should().Be("COR_RACA");
        envelope.RootElement.GetProperty("operadoresAdmitidos").EnumerateArray()
            .Select(static o => o.GetProperty("codigo").GetString()).Should().Equal("IGUAL", "DIFERENTE", "ALGUM_DE", "NENHUM_DE");
    }

    [Fact(DisplayName = "Origem desconhecida no filtro da lista é recusada com 400")]
    public async Task Listar_OrigemDesconhecida_RecusaCom400()
    {
        using HttpClient client = Fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Get, $"{Base}?origem=declarado", corpo: null);

        await ProblemaRegistrado.ConferirAsync(resposta, HttpStatusCode.BadRequest,
            "uniplus.configuracao.fato_candidato.filtro_de_origem_invalida",
            "Origem do filtro de dados do candidato inválida",
            "Origem inválida. Use um dos códigos publicados para a origem do dado do candidato.");
    }

    [Fact(DisplayName = "Código já usado, inclusive por fato de sistema, é recusado com conflito")]
    public async Task Criar_CodigoExistente_Retorna409()
    {
        using HttpClient client = Fixture.Factory.CreateClient();

        (await EnviarAsync(client, HttpMethod.Post, Base, FatoBooleano("COR_RACA")))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "Fato de sistema não é desativado pela manutenção")]
    public async Task Desativar_FatoDeSistema_Retorna422()
    {
        using HttpClient client = Fixture.Factory.CreateClient();

        (await EnviarAsync(client, HttpMethod.Delete, $"{Base}/{IdDeCorRaca}", corpo: null))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private static object Descritivo(string nome, string descricao, string componenteExibicao) =>
        new { nome, descricao, componenteExibicao };

    private static object FatoSemDescricao() => new
    {
        codigo = CodigoUnico(),
        nome = "Vínculo com outra instituição pública",
        dominio = "SIM_OU_NAO",
        cardinalidade = "VALOR_UNICO",
        pontoResolucao = "HABILITACAO",
        escopo = "CANDIDATO",
        classificacaoProtecao = "PESSOAL",
        finalidadeTratamento = "Verificação dos requisitos de matrícula do processo seletivo.",
        hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
    };

    private static async Task<JsonDocument> ListarAsync(HttpClient client, string filtro)
    {
        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Get, $"{Base}{filtro}", corpo: null);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
    }

    private static void ConferirInativosDeclarados(JsonDocument lista, Guid id)
    {
        JsonElement[] fatos = [.. lista.RootElement.EnumerateArray().Select(static item => item.GetProperty("fato"))];
        fatos.Should().Contain(fato => fato.GetProperty("id").GetGuid() == id
            && fato.GetProperty("descritivo").GetProperty("nome").GetString() == "Nome editado");
        fatos.Should().OnlyContain(fato => !fato.GetProperty("ativo").GetBoolean()
            && fato.GetProperty("definicao").GetProperty("resolucao").GetProperty("origem").GetProperty("codigo").GetString() == "DECLARADO");
        fatos.Should().NotContain(fato => fato.GetProperty("codigo").GetString() == "COR_RACA");
    }

    private static async Task<ApresentacaoLida> LerApresentacaoAsync(HttpClient client, Guid id)
    {
        using JsonDocument fato = await ObterAsync(client, id);
        return new ApresentacaoLida(
            CodigoDoComponente(Em(fato, "descritivo", "componenteExibicao")),
            Em(fato, "descritivo", "descricao").GetString()!,
            Em(fato, "definicao", "protecao", "aprovacao", "situacao").GetString()!);
    }

    private static string? CodigoDoComponente(JsonElement componente) =>
        componente.ValueKind == JsonValueKind.Null ? null : componente.GetProperty("codigo").GetString();

    private sealed record ApresentacaoLida(string? Componente, string Descricao, string Situacao);
}
