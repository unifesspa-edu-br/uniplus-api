namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ProtecaoDoCatalogo;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>O apoio dos testes da proteção do catálogo pelo host real (ADR-0153).</summary>
public abstract class ProtecaoDoCatalogoTestBase
{
    private protected const string Base = "/api/configuracao/admin/protecao-do-catalogo";
    private protected const string Tipos = "/api/configuracao/admin/tipos-documento";
    private protected const string Fatos = "/api/configuracao/admin/fatos-candidato";
    private protected const string Caroline = "caroline.lima";
    private protected const string Paulo = "paulo.conceicao";
    private protected static readonly Guid IdDeQuilombola = Guid.Parse("fa700000-0000-7000-8000-000000000002");

    private protected ProtecaoDoCatalogoTestBase(ConfiguracaoEndpointFixture fixture) => Fixture = fixture;

    private protected ConfiguracaoEndpointFixture Fixture { get; }

    private protected static object DefinicaoDe(JsonElement fato, string? dominio = null, string? hipoteseLegal = null) => new
    {
        pontoResolucao = fato.GetProperty("pontoResolucao").GetString(),
        classificacaoProtecao = fato.GetProperty("classificacaoProtecao").GetString(),
        finalidadeTratamento = fato.GetProperty("finalidadeTratamento").GetString(),
        hipoteseLegal = hipoteseLegal ?? fato.GetProperty("hipoteseLegal").GetString(),
        dominio = dominio ?? fato.GetProperty("dominio").GetString(),
        cardinalidade = fato.GetProperty("cardinalidade").GetString(),
        escopo = fato.GetProperty("escopo").GetString(),
    };

    // Caroline, a pessoa do token padrão, é Representante do controlador e aprova a proteção do catálogo.
    private protected async Task TornarCarolineRepresentanteAsync()
    {
        await ConcessoesDeTeste.RemoverDaPessoaAsync(Fixture.Factory.Services, Caroline);
        await ConcessoesDeTeste.ConcederGovernancaAsync(
            Fixture.Factory.Services, Caroline, TipoDeGovernanca.RepresentanteDoControlador, "caroline.lima@unifesspa.edu.br");
    }

    // Paulo mantém ou consulta os cadastros pedidos, sem nenhuma outra concessão.
    private protected async Task TornarPauloMantenedorAsync(params string[] permissoes)
    {
        await ConcessoesDeTeste.RemoverDaPessoaAsync(Fixture.Factory.Services, Paulo);
        await ConcessoesDeTeste.ConcederPermissoesAsync(Fixture.Factory.Services, Paulo, permissoes);
    }

    // A requisição como Paulo, sem o papel de administrador da plataforma.
    private protected static async Task<HttpResponseMessage> EnviarComoPauloAsync(HttpClient client, HttpMethod metodo, string url, object? corpo = null)
    {
        using HttpRequestMessage request = new(metodo, new Uri(url, UriKind.Relative)) { Content = corpo is null ? null : JsonContent.Create(corpo) };
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, Paulo);
        if (metodo != HttpMethod.Get)
        {
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        }

        return await client.SendAsync(request);
    }

    private protected static async Task<string?> SituacaoAsync(HttpClient client, Guid id) =>
        (await ObterDoCatalogoAsync(client, id)).GetProperty("aprovacao").GetProperty("situacao").GetString();

    private protected static async Task<Guid> CadastrarTipoAsync(HttpClient client, string classificacao)
    {
        HttpResponseMessage criar = await EnviarAsync(client, HttpMethod.Post, Tipos, new
        {
            codigo = $"TIPO_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}",
            nome = "Laudo médico complementar",
            categoria = "SAUDE",
            classificacaoProtecao = classificacao,
            finalidadeTratamento = "Comprovação documental do candidato.",
            hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
        });
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        return await criar.Content.ReadFromJsonAsync<Guid>();
    }

    // A decisão leva a proteção que o aprovador conferiu, como a listagem a mostra.
    private protected static async Task<HttpResponseMessage> DecidirAsync(
        HttpClient client, string cadastro, Guid itemId, string decisao, string? recomendada = null, bool protecaoDesatualizada = false, string? justificativa = null)
    {
        JsonElement item = await ObterDoCatalogoAsync(client, itemId);
        return await EnviarAsync(client, HttpMethod.Post, Base, CorpoDaDecisao(item, cadastro, decisao) with
        {
            ClassificacaoProtecao = protecaoDesatualizada ? "SENSIVEL" : item.GetProperty("classificacaoProtecao").GetString(),
            ClassificacaoRecomendada = recomendada,
            Justificativa = justificativa,
        });
    }

    private protected static DecisaoPedida CorpoDaDecisao(JsonElement item, string cadastro, string decisao) =>
        new(cadastro, item.GetProperty("id").GetGuid(), decisao, item.GetProperty("classificacaoProtecao").GetString(),
            item.GetProperty("hipoteseLegal").GetString(), item.GetProperty("finalidadeTratamento").GetString());

    private protected static Task<HttpResponseMessage> SolicitarAsync(HttpClient client, params (string Cadastro, Guid Id)[] itens) =>
        EnviarAsync(client, HttpMethod.Post, $"{Base}/solicitacoes", new { itens = itens.Select(i => new { cadastro = i.Cadastro, i.Id }) });

    private protected static Task<HttpResponseMessage> SolicitarAsync(HttpClient client, string cadastro, Guid id) => SolicitarAsync(client, (cadastro, id));

    private protected static async Task<JsonElement> ObterDoCatalogoAsync(HttpClient client, Guid id) =>
        (await ListarAsync(client)).Single(item => item.GetProperty("id").GetGuid() == id);

    private protected static async Task<List<JsonElement>> ListarAsync(HttpClient client)
    {
        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Get, Base);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return [.. (await resposta.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()];
    }

    private protected static async Task<JsonElement> ObterFatoAsync(HttpClient client, Guid id)
    {
        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Get, $"{Fatos}/{id}");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await resposta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private protected static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, HttpMethod metodo, string url, object? corpo = null)
    {
        using HttpRequestMessage request = new(metodo, new Uri(url, UriKind.Relative)) { Content = corpo is null ? null : JsonContent.Create(corpo) };
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        if (metodo != HttpMethod.Get)
        {
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        }

        return await client.SendAsync(request);
    }

    private protected sealed record DecisaoPedida(
        string Cadastro, Guid ItemId, string Decisao, string? ClassificacaoProtecao, string? HipoteseLegal, string? FinalidadeTratamento)
    {
        public string? ClassificacaoRecomendada { get; init; }

        public string? Justificativa { get; init; }
    }
}
