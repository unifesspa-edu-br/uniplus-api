namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposEtapa;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure.TiposConceitoHttpDeTeste;

/// <summary>
/// Os tipos de conceito que um tipo de etapa admite: a lista é obrigatória, ainda que vazia, é substituída por
/// inteiro a cada edição e só aceita conceito existente, ativo e informado uma vez.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class TipoEtapaConceitosAdmitidosEndpointTests
{
    private const string Prefixo = "uniplus.configuracao.tipo_etapa.";
    private const string Rota = "/api/configuracao/admin/tipos-etapa";

    private readonly ConfiguracaoEndpointFixture _fixture;

    public TipoEtapaConceitosAdmitidosEndpointTests(ConfiguracaoEndpointFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "POST com conceitos válidos cria o tipo, e o GET devolve código, nome e natureza de cada conceito pelo código")]
    public async Task Criar_ComConceitosValidos_DevolveOsConceitosNaLeitura()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        string prefixo = CodigoUnico();
        await CriarAsync(client, Numerico($"{prefixo}_NOTA"));
        await CriarAsync(client, Categorico($"{prefixo}_PARECER"));

        Guid id = await CriarTipoAsync(client, [$"{prefixo}_PARECER", $"{prefixo}_NOTA"]);

        using JsonDocument tipo = await LerTipoAsync(client, id);
        tipo.RootElement.GetProperty("conceitosAdmitidos").EnumerateArray()
            .Select(conceito => (conceito.GetProperty("codigo").GetString(), conceito.GetProperty("nome").GetString(), conceito.GetProperty("natureza").GetProperty("codigo").GetString()))
            .Should().Equal(
                ($"{prefixo}_NOTA", "Nota da entrevista", "NUMERICO"),
                ($"{prefixo}_PARECER", "Parecer da análise", "CATEGORICO"));
    }

    [Fact(DisplayName = "A listagem pública traz os conceitos admitidos de cada tipo de etapa")]
    public async Task Listar_TrazOsConceitosAdmitidos()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        string conceito = CodigoUnico();
        await CriarAsync(client, Numerico(conceito));
        Guid id = await CriarTipoAsync(client, [conceito]);

        (await CodigosNaListagemAsync(client, id)).Should().Equal(conceito);
    }

    [Fact(DisplayName = "A lista vazia é válida: o tipo nasce sem conceito admitido")]
    public async Task Criar_ListaVazia_CriaSemConceitos()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        Guid id = await CriarTipoAsync(client, []);

        using JsonDocument tipo = await LerTipoAsync(client, id);
        tipo.RootElement.GetProperty("conceitosAdmitidos").GetArrayLength().Should().Be(0);
    }

    [Fact(DisplayName = "PUT substitui o conjunto de conceitos admitidos, sem acumular o anterior")]
    public async Task Atualizar_SubstituiOConjunto()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        string anterior = CodigoUnico();
        string novo = CodigoUnico();
        await CriarAsync(client, Numerico(anterior));
        await CriarAsync(client, Categorico(novo));
        Guid id = await CriarTipoAsync(client, [anterior]);

        using HttpResponseMessage resposta = await AtualizarTipoAsync(client, id, [novo]);

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await CodigosAdmitidosAsync(client, id)).Should().Equal(novo);
    }

    [Fact(DisplayName = "PUT com lista vazia deixa o tipo sem conceito admitido")]
    public async Task Atualizar_ListaVazia_LimpaOConjunto()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        string conceito = CodigoUnico();
        await CriarAsync(client, Numerico(conceito));
        Guid id = await CriarTipoAsync(client, [conceito]);

        using HttpResponseMessage resposta = await AtualizarTipoAsync(client, id, []);

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await CodigosAdmitidosAsync(client, id)).Should().BeEmpty();
    }

    [Fact(DisplayName = "O código do conceito com espaços nas pontas é resolvido contra o catálogo")]
    public async Task Criar_CodigoComEspacosNasPontas_EResolvido()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        string conceito = CodigoUnico();
        await CriarAsync(client, Numerico(conceito));

        Guid id = await CriarTipoAsync(client, [$"  {conceito} "]);

        (await CodigosAdmitidosAsync(client, id)).Should().Equal(conceito);
    }

    [Theory(DisplayName = "POST e PUT sem conceitosAdmitidos retornam 422 conceitos_admitidos_obrigatorio")]
    [InlineData("POST")]
    [InlineData("PUT")]
    public async Task Gravar_SemConceitosAdmitidos_Retorna422(string metodo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarTipoAsync(client, []);
        Dictionary<string, object?> corpo = CorpoDoTipo(CodigoUnico(), []).Sem("conceitosAdmitidos").Com("id", id);

        using HttpResponseMessage resposta = metodo == "POST"
            ? await EnviarAsync(client, HttpMethod.Post, Rota, corpo)
            : await EnviarAsync(client, HttpMethod.Put, $"{Rota}/{id}", corpo);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ErrosAsync(resposta)).Should().Equal(("conceitosAdmitidos", $"{Prefixo}conceitos_admitidos_obrigatorio"));
    }

    [Theory(DisplayName = "POST com conceito inexistente, inativo ou repetido retorna 422 com o código da espécie")]
    [InlineData("inexistente", "conceito_inexistente")]
    [InlineData("inativo", "conceito_inativo")]
    [InlineData("duplicado", "conceito_duplicado")]
    public async Task Criar_ConceitoRecusado_Retorna422(string caso, string codigo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        string[] conceitos = await ConceitosDoCasoAsync(client, caso);

        using HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Post, Rota, CorpoDoTipo(CodigoUnico(), conceitos));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ErrosAsync(resposta)).Should().Equal(("conceitosAdmitidos", $"{Prefixo}{codigo}"));
    }

    [Theory(DisplayName = "PUT com conceito inexistente, inativo ou repetido retorna 422 e mantém o conjunto anterior")]
    [InlineData("inexistente", "conceito_inexistente")]
    [InlineData("inativo", "conceito_inativo")]
    [InlineData("duplicado", "conceito_duplicado")]
    public async Task Atualizar_ConceitoRecusado_Retorna422EMantemOConjunto(string caso, string codigo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        string mantido = CodigoUnico();
        await CriarAsync(client, Numerico(mantido));
        Guid id = await CriarTipoAsync(client, [mantido]);
        string[] conceitos = await ConceitosDoCasoAsync(client, caso);

        using HttpResponseMessage resposta = await AtualizarTipoAsync(client, id, conceitos);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ErrosAsync(resposta)).Should().Equal(("conceitosAdmitidos", $"{Prefixo}{codigo}"));
        (await CodigosAdmitidosAsync(client, id)).Should().Equal(mantido);
    }

    [Fact(DisplayName = "Conceito inexistente e repetido no mesmo pedido voltam como duas violações, sem ecoar o código informado")]
    public async Task Criar_RecusasDeEspeciesDiferentes_AcumulamSemEcoarOCodigo()
    {
        string inexistente = CodigoUnico();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await EnviarAsync(
            client, HttpMethod.Post, Rota, CorpoDoTipo(CodigoUnico(), [inexistente, inexistente]));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        string corpo = await resposta.Content.ReadAsStringAsync();
        corpo.Should().NotContain(inexistente);
        using JsonDocument problema = JsonDocument.Parse(corpo);
        problema.RootElement.GetProperty("errors").EnumerateArray().Select(erro => erro.GetProperty("code").GetString())
            .Should().Equal($"{Prefixo}conceito_inexistente", $"{Prefixo}conceito_duplicado");
    }

    [Fact(DisplayName = "A recusa por conceito inexistente não grava o tipo: o código continua livre")]
    public async Task Criar_ConceitoRecusado_NaoReservaOCodigo()
    {
        string codigo = CodigoUnico();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage recusada = await EnviarAsync(client, HttpMethod.Post, Rota, CorpoDoTipo(codigo, [CodigoUnico()]));
        using HttpResponseMessage valida = await EnviarAsync(client, HttpMethod.Post, Rota, CorpoDoTipo(codigo, []));

        recusada.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        valida.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private static Dictionary<string, object?> CorpoDoTipo(string codigo, string[] conceitos) => new()
    {
        ["codigo"] = codigo,
        ["nome"] = "Etapa avaliada por conceito",
        ["admitePontuacao"] = true,
        ["admiteEliminacao"] = true,
        ["exigeCorrecaoIndependente"] = false,
        ["conceitosAdmitidos"] = conceitos,
    };

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, HttpMethod metodo, string rota, object corpo)
    {
        using HttpRequestMessage requisicao = Autenticada(metodo, rota);
        requisicao.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        requisicao.Content = JsonContent.Create(corpo);
        return await client.SendAsync(requisicao);
    }

    private static async Task<Guid> CriarTipoAsync(HttpClient client, string[] conceitos)
    {
        using HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Post, Rota, CorpoDoTipo(CodigoUnico(), conceitos));
        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        return await resposta.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<HttpResponseMessage> AtualizarTipoAsync(HttpClient client, Guid id, string[] conceitos) =>
        await EnviarAsync(client, HttpMethod.Put, $"{Rota}/{id}", CorpoDoTipo(CodigoUnico(), conceitos).Sem("codigo").Com("id", id));

    private static async Task<string[]> ConceitosDoCasoAsync(HttpClient client, string caso)
    {
        string codigo = CodigoUnico();
        return caso switch
        {
            "inexistente" => [codigo],
            "inativo" => [await CadastrarDesativadoAsync(client, codigo)],
            "duplicado" => [await CadastrarAsync(client, codigo), codigo],
            _ => throw new InvalidOperationException($"Caso de teste inesperado: {caso}"),
        };
    }

    private static async Task<string> CadastrarAsync(HttpClient client, string codigo)
    {
        await CriarAsync(client, Numerico(codigo));
        return codigo;
    }

    private static async Task<string> CadastrarDesativadoAsync(HttpClient client, string codigo)
    {
        Guid id = await CriarAsync(client, Numerico(codigo));
        (await DesativarAsync(client, id)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        return codigo;
    }

    private static async Task<JsonDocument> LerTipoAsync(HttpClient client, Guid id)
    {
        using HttpResponseMessage resposta = await client.GetAsync(new Uri($"/api/configuracao/tipos-etapa/{id}", UriKind.Relative));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await LerAsync(resposta);
    }

    private static async Task<string?[]> CodigosAdmitidosAsync(HttpClient client, Guid id)
    {
        using JsonDocument tipo = await LerTipoAsync(client, id);
        return CodigosDos(tipo.RootElement);
    }

    private static async Task<string?[]> CodigosNaListagemAsync(HttpClient client, Guid id)
    {
        string? url = "/api/configuracao/tipos-etapa?limit=100";
        while (url is not null)
        {
            using HttpResponseMessage pagina = await client.GetAsync(new Uri(url, UriKind.RelativeOrAbsolute));
            string?[]? codigos = await CodigosDoTipoNaPaginaAsync(pagina, id);
            if (codigos is not null)
            {
                return codigos;
            }

            url = CampusHttpDeTeste.LinkRel(pagina, "next");
        }

        throw new InvalidOperationException("O tipo de etapa criado não apareceu na listagem pública.");
    }

    private static async Task<string?[]?> CodigosDoTipoNaPaginaAsync(HttpResponseMessage pagina, Guid id)
    {
        using JsonDocument lista = await LerAsync(pagina);
        JsonElement[] doTipo = [.. lista.RootElement.EnumerateArray().Where(item => item.GetProperty("id").GetGuid() == id)];
        return doTipo.Length > 0 ? CodigosDos(doTipo[0]) : null;
    }

    private static string?[] CodigosDos(JsonElement tipo) =>
        [.. tipo.GetProperty("conceitosAdmitidos").EnumerateArray().Select(conceito => conceito.GetProperty("codigo").GetString())];

    private static async Task<IReadOnlyList<(string? Campo, string? Codigo)>> ErrosAsync(HttpResponseMessage resposta)
    {
        using JsonDocument problema = await LerAsync(resposta);
        return [.. problema.RootElement.GetProperty("errors").EnumerateArray()
            .Select(erro => (erro.GetProperty("field").GetString(), erro.GetProperty("code").GetString()))];
    }
}
