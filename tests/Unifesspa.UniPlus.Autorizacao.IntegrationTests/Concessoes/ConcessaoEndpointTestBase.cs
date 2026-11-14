namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Concessoes;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// O apoio dos testes de concessão pelo endpoint real, contra Postgres efêmero. As concessões nascem pelo repositório,
/// porque a criação pertence ao pedido aprovado e à instalação. As personas são as do portal: Diogo Souza e Isabella
/// Figueiredo são gestores de acessos (Isabella também é Representante do controlador), Betina Neves é o terceiro gestor
/// de acessos e Leandro Viana é o beneficiário.
/// </summary>
public abstract class ConcessaoEndpointTestBase
{
    private protected const string Rota = "/api/autorizacao/concessoes";
    private protected const string RotaPerfis = "/api/autorizacao/perfis";
    private protected const string Administrar = "autorizacao:concessoes:administrar";
    private protected const string ConsultarCatalogo = "autorizacao:permissoes:consultar-catalogo";
    private protected const string Motivo = "Mudou de função na unidade.";
    private protected const string EmailDoBeneficiario = "beneficiario@uniplus-test.local";

    private protected static readonly Guid GestorDeAcessosId = new("019a0000-0000-7000-8000-000000000001");
    private protected static readonly Guid RepresentanteDoControladorId = new("019a0000-0000-7000-8000-000000000002");

    private protected static readonly Identidade Diogo = Pessoa("diogo.souza");
    private protected static readonly Identidade Isabella = Pessoa("isabella.figueiredo");
    private protected static readonly Identidade Betina = Pessoa("betina.neves");
    private protected static readonly Identidade Leandro = Pessoa("leandro.viana");

    private protected ConcessaoEndpointTestBase(AutorizacaoEndpointFixture fixture) => Fixture = fixture;

    private protected AutorizacaoEndpointFixture Fixture { get; }

    private protected static Identidade Pessoa(string sub) => new(TestAuthHandler.DefaultIssuer, sub);

    private protected static async Task<HttpResponseMessage> EnviarAsync(
        HttpClient client, HttpMethod metodo, string url, object? corpo = null)
    {
        using HttpRequestMessage request = new(metodo, new Uri(url, UriKind.Relative));
        if (corpo is not null)
        {
            request.Content = JsonContent.Create(corpo);
        }

        if (metodo != HttpMethod.Get)
        {
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        }

        return await client.SendAsync(request);
    }

    private protected static async Task<JsonDocument> LerAsync(HttpClient client, string url)
    {
        using HttpResponseMessage resposta = await client.GetAsync(new Uri(url, UriKind.Relative));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
    }

    private protected static async Task<Guid[]> IdsAsync(HttpClient client, string filtro)
    {
        using JsonDocument pagina = await LerAsync(client, Rota + filtro);
        JsonElement itens = pagina.RootElement.ValueKind == JsonValueKind.Array
            ? pagina.RootElement
            : pagina.RootElement.GetProperty("items");

        return [.. itens.EnumerateArray().Select(i => i.GetProperty("id").GetGuid())];
    }

    private protected Task<Guid> CriarPerfilComumAsync() =>
        ConcessoesDeTeste.CriarPerfilAsync(Fixture.Factory.Services, "teste:inscricoes:operar");

    // A vigência só vence com o tempo: o teste a recua no banco em vez de esperar.
    private protected async Task EnvelhecerAsync(Guid id)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE autorizacao.concessoes SET concedida_em = now() - interval '1 hour', valida_ate = now() - interval '1 minute' WHERE id = {0}", id);
    }

    private protected async Task<Guid> ConcederAsync(
        Guid perfilId, Identidade beneficiario, EscopoDaConcessao escopo, int? validaAteDaquiAMinutos = null)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        Concessao concessao = await MontarAsync(scope, perfilId, beneficiario, escopo, validaAteDaquiAMinutos);
        IConcessaoRepository repositorio = scope.ServiceProvider.GetRequiredService<IConcessaoRepository>();
        await repositorio.AdicionarAsync(concessao);
        await scope.ServiceProvider.GetRequiredService<UniPlusDbContext>().SaveChangesAsync();
        return concessao.Id;
    }

    private protected async Task<Exception> FalhaAoConcederAsync(Guid perfilId, Identidade beneficiario, EscopoDaConcessao escopo)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        Concessao concessao = await MontarAsync(scope, perfilId, beneficiario, escopo);
        IConcessaoRepository repositorio = scope.ServiceProvider.GetRequiredService<IConcessaoRepository>();
        await repositorio.AdicionarAsync(concessao);

        return await Assert.ThrowsAnyAsync<Exception>(
            () => scope.ServiceProvider.GetRequiredService<UniPlusDbContext>().SaveChangesAsync());
    }

    private protected static async Task<Concessao> MontarAsync(
        AsyncServiceScope scope, Guid perfilId, Identidade beneficiario, EscopoDaConcessao escopo, int? validaAteDaquiAMinutos = null)
    {
        IConcessaoRepository repositorio = scope.ServiceProvider.GetRequiredService<IConcessaoRepository>();
        TimeProvider relogio = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        Perfil perfil = (await repositorio.ObterParaConcederAsync(perfilId))!;

        return ConcessaoBuilder.DoPerfil(perfil, scope.ServiceProvider.GetRequiredService<ICatalogoDePermissoesConsultavel>())
            .Para(beneficiario, EmailDoBeneficiario)
            .NoEscopo(escopo)
            .ConcedidaPor(Diogo)
            .Em(relogio.GetUtcNow())
            .ValidaAte(validaAteDaquiAMinutos is { } minutos ? relogio.GetUtcNow().AddMinutes(minutos) : null)
            .Construir();
    }

    private protected async Task LimparConcessoesAsync(Guid perfilId)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM autorizacao.liberacoes_da_reclassificacao WHERE perfil_id = {0}", perfilId);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM autorizacao.bases_da_permissao_na_concessao WHERE perfil_id = {0}", perfilId);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM autorizacao.concessoes WHERE perfil_id = {0}", perfilId);
    }

    // O token só identifica a pessoa; o que ela pode vem da concessão gravada, como em produção.
    private protected Task<HttpClient> ClienteDoGestorAsync(Identidade pessoa) =>
        ClienteDeGovernancaAsync(pessoa, TipoDeGovernanca.GestorDeAcessos);

    private protected Task<HttpClient> ClienteDoRepresentanteAsync(Identidade pessoa) =>
        ClienteDeGovernancaAsync(pessoa, TipoDeGovernanca.RepresentanteDoControlador);

    private protected async Task<HttpClient> ClienteDeGovernancaAsync(Identidade pessoa, TipoDeGovernanca governanca)
    {
        await ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, pessoa.Sub, governanca);
        return Cliente(pessoa);
    }

    private protected async Task<HttpClient> ClienteComPermissoesAsync(Identidade pessoa, params string[] permissoes)
    {
        await ConcessoesDeTeste.ConcederPermissoesAsync(Fixture.Factory.Services, pessoa.Sub, permissoes);
        return Cliente(pessoa);
    }

    private protected HttpClient Cliente(Identidade pessoa)
    {
        HttpClient client = Fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, pessoa.Sub);
        client.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, pessoa.Sub);
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "teste@unifesspa.edu.br");
        return client;
    }

    private protected static Task<HttpResponseMessage> RevogarAsync(HttpClient client, Guid id, string? confirmacaoEmail = EmailDoBeneficiario) =>
        EnviarAsync(client, HttpMethod.Post, $"{Rota}/{id}/revogacao", new { motivo = Motivo, confirmacaoEmail });

    // Os status das requisições simultâneas; as respostas são descartadas depois de lidas.
    private protected static async Task<HttpStatusCode[]> StatusDasSimultaneasAsync(params Task<HttpResponseMessage>[] envios)
    {
        HttpResponseMessage[] respostas = await Task.WhenAll(envios);
        HttpStatusCode[] status = [.. respostas.Select(static r => r.StatusCode)];
        foreach (HttpResponseMessage resposta in respostas)
        {
            resposta.Dispose();
        }

        return status;
    }
}
