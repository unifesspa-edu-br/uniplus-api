namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FatosCandidato;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>Rota, fixture e montagem de requisições comuns aos testes do contrato HTTP da manutenção de fatos.</summary>
public abstract class FatoCandidatoAdminEndpointTestBase
{
    protected const string Base = "/api/configuracao/admin/fatos-candidato";
    protected static readonly Guid IdDeCorRaca = Guid.Parse("fa700000-0000-7000-8000-000000000001");

    protected ConfiguracaoEndpointFixture Fixture { get; }

    protected FatoCandidatoAdminEndpointTestBase(ConfiguracaoEndpointFixture fixture)
    {
        Fixture = fixture;
    }

    protected static object DefinicaoBooleana() => new
    {
        dominio = "BOOLEANO",
        cardinalidade = "ESCALAR",
        pontoResolucao = "INSCRICAO",
        escopo = "CANDIDATO",
        classificacaoProtecao = "SENSIVEL",
        finalidadeTratamento = "Enquadramento na reserva de vagas.",
        hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
    };

    protected static object DefinicaoBooleanaComFonteDeValores() => new
    {
        dominio = "BOOLEANO",
        cardinalidade = "ESCALAR",
        fonteValores = "GLOBAL",
        pontoResolucao = "INSCRICAO",
        escopo = "CANDIDATO",
        classificacaoProtecao = "PESSOAL",
        finalidadeTratamento = "Verificação dos requisitos de matrícula do processo seletivo.",
        hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
    };

    protected static object DefinicaoCategorica() => new
    {
        dominio = "CATEGORICO",
        cardinalidade = "ESCALAR",
        fonteValores = "GLOBAL",
        pontoResolucao = "INSCRICAO",
        escopo = "CANDIDATO",
        classificacaoProtecao = "PESSOAL",
        finalidadeTratamento = "Verificação dos requisitos de matrícula do processo seletivo.",
        hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
    };

    protected static object FatoBooleano(string codigo) => new
    {
        codigo,
        nome = "Vínculo com outra instituição pública",
        descricao = "Descrição do fato.",
        dominio = "BOOLEANO",
        cardinalidade = "ESCALAR",
        pontoResolucao = "HABILITACAO",
        escopo = "CANDIDATO",
        classificacaoProtecao = "PESSOAL",
        finalidadeTratamento = "Verificação dos requisitos de matrícula do processo seletivo.",
        hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
    };

    protected static object FatoCategorico() => new
    {
        codigo = CodigoUnico(),
        nome = "Forma de conclusão",
        descricao = "Descrição do fato.",
        dominio = "CATEGORICO",
        cardinalidade = "ESCALAR",
        fonteValores = "GLOBAL",
        pontoResolucao = "INSCRICAO",
        escopo = "CANDIDATO",
        classificacaoProtecao = "PESSOAL",
        finalidadeTratamento = "Verificação dos requisitos do processo seletivo.",
        hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
    };

    protected static object FatoBooleanoDeMembro(string codigo) => new
    {
        codigo,
        nome = "Menor sob guarda",
        descricao = "Descrição do fato.",
        dominio = "BOOLEANO",
        cardinalidade = "ESCALAR",
        pontoResolucao = "HABILITACAO",
        escopo = "MEMBRO_GRUPO",
        classificacaoProtecao = "PESSOAL",
        finalidadeTratamento = "Composição familiar.",
        hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
    };

    protected static object FatoCategoricoDeMembro(string codigo) => new
    {
        codigo,
        nome = "Categoria de renda",
        descricao = "Descrição do fato.",
        dominio = "CATEGORICO",
        cardinalidade = "ESCALAR",
        fonteValores = "GLOBAL",
        pontoResolucao = "HABILITACAO",
        escopo = "MEMBRO_GRUPO",
        classificacaoProtecao = "PESSOAL",
        finalidadeTratamento = "Composição familiar.",
        hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
    };

    protected static object DerivadoCategorico() => new
    {
        codigo = CodigoUnico(),
        nome = "Perfil de conclusão",
        descricao = "Descrição do fato.",
        dominio = "CATEGORICO",
        pontoResolucao = "HABILITACAO",
        escopo = "CANDIDATO",
        classificacaoProtecao = "PESSOAL",
        finalidadeTratamento = "Verificação dos requisitos do processo seletivo.",
        hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
    };

    protected static object Agregado(string fatoDeMembro, string? codigo = null) => new
    {
        codigo = codigo ?? CodigoUnico(),
        nome = "Agregado sobre o fato de membro",
        descricao = "Descrição do fato.",
        fatoDeMembro,
        pontoResolucao = "HABILITACAO",
        classificacaoProtecao = "PESSOAL",
        finalidadeTratamento = "Composição familiar.",
        hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
    };

    protected static string CodigoUnico() => $"FATO_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";

    /// <summary>Cadastra pela rota e devolve o identificador; o cadastro tem de responder 201.</summary>
    protected static async Task<Guid> CadastrarAsync(HttpClient client, object corpo, string rota = "")
    {
        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Post, $"{Base}{rota}", corpo);
        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
        return await resposta.Content.ReadFromJsonAsync<Guid>();
    }

    protected static Task AcrescentarValorAsync(HttpClient client, Guid id, string codigo, string descricao, int ordem) =>
        ConferirAsync(client, HttpMethod.Post, $"{Base}/{id}/valores", new { codigo, descricao, ordem }, HttpStatusCode.NoContent);

    /// <summary>Envia a requisição e confere o status da resposta.</summary>
    protected static async Task ConferirAsync(
        HttpClient client, HttpMethod metodo, string url, object? corpo, HttpStatusCode esperado, string? motivo = null)
    {
        using HttpResponseMessage resposta = await EnviarAsync(client, metodo, url, corpo);
        resposta.StatusCode.Should().Be(esperado, motivo);
    }

    /// <summary>O fato da resposta de manutenção: o <c>fato</c> do envelope, sem os operadores.</summary>
    protected static async Task<JsonDocument> ObterAsync(HttpClient client, Guid id)
    {
        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Get, $"{Base}/{id}", corpo: null);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument envelope = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return JsonDocument.Parse(envelope.RootElement.GetProperty("fato").GetRawText());
    }

    /// <summary>A propriedade do fato no caminho informado, a partir da raiz.</summary>
    protected static JsonElement Em(JsonDocument fato, params string[] caminho)
    {
        ArgumentNullException.ThrowIfNull(fato);
        ArgumentNullException.ThrowIfNull(caminho);
        return caminho.Aggregate(fato.RootElement, static (elemento, propriedade) => elemento.GetProperty(propriedade));
    }

    protected static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, HttpMethod metodo, string url, object? corpo)
    {
        ArgumentNullException.ThrowIfNull(client);
        using HttpRequestMessage request = Requisicao(metodo, url, corpo);
        return await client.SendAsync(request);
    }

    private static HttpRequestMessage Requisicao(HttpMethod metodo, string url, object? corpo)
    {
        HttpRequestMessage request = new(metodo, new Uri(url, UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        if (metodo != HttpMethod.Get)
        {
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        }

        request.Content = corpo is null ? null : JsonContent.Create(corpo);
        return request;
    }
}
