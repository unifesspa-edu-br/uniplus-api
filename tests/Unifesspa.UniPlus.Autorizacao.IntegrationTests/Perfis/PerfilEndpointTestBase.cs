namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>Constantes, semeadura, acessos ao banco e chamadas HTTP comuns aos testes de borda dos perfis de acesso.</summary>
public abstract class PerfilEndpointTestBase
{
    internal const string Rota = "/api/autorizacao/perfis";
    internal const string ConsultarCatalogo = "autorizacao:permissoes:consultar-catalogo";
    internal const string ManterMotivos = "selecao:motivos-de-decisao:manter";
    internal const string OperarInscricoes = "teste:inscricoes:operar";
    internal const string AnalisarAtendimento = "teste:atendimento:analisar";
    internal const string GestorDeAcessosId = "019a0000-0000-7000-8000-000000000001";

    internal const string PermissaoManter = "autorizacao:perfis:manter";
    internal const string Diogo = "diogo.souza";
    internal const string OutroGestor = "isabella.figueiredo";

    protected PerfilEndpointTestBase(AutorizacaoEndpointFixture fixture) => Fixture = fixture;

    internal AutorizacaoEndpointFixture Fixture { get; }

    internal const string Descricao = "Perfil de teste do cadastro de acesso.";

    internal static string Nome() => $"Perfil {Guid.NewGuid():N}";

    internal static object Corpo(string nome, string[] permissoes) =>
        new { nome, descricao = Descricao, permissoes, protocolo = "23479.000123/2026-11", tamanhoDoDocumento = 2048 };

    internal static object ComMotivoDaRemocao(object corpo) =>
        new Dictionary<string, object?>(
            corpo.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(corpo)))
        {
            ["motivoDaRemocao"] = "Retirada de permissão que o perfil não usa mais.",
        };

    internal static Envio LerEnvio(string json)
    {
        using JsonDocument documento = JsonDocument.Parse(json);
        JsonElement raiz = documento.RootElement;

        return new Envio(raiz.GetProperty("perfilId").GetGuid(), raiz.GetProperty("versaoId").GetGuid(), raiz.GetProperty("documentoId").GetGuid());
    }

    internal sealed record Envio(Guid PerfilId, Guid VersaoId, Guid DocumentoId);

    internal Task<Guid> SemearPerfilVigenteAsync(params string[] permissoes) =>
        ConcessoesDeTeste.CriarPerfilAsync(Fixture.Factory.Services, permissoes);

    internal async Task<Guid> VersaoVigenteAsync(Guid perfilId)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();

        return await db.VersoesDosPerfis.Where(v => v.PerfilId == perfilId && v.Situacao == SituacaoDaVersao.Vigente).Select(v => v.Id).SingleAsync();
    }

    internal async Task<int> ContarLeiturasAsync(Guid perfilId)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();

        return await db.LeiturasDosDocumentosDasVersoes.CountAsync(l => db.DocumentosDasVersoes.Any(d => d.Id == l.DocumentoId && d.PerfilId == perfilId));
    }

    internal async Task ExecutarAsync(string sql, params object[] parametros)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Database.ExecuteSqlRawAsync(sql, parametros);
    }

    internal Task InserirVersaoAsync(Guid perfilId, int numero, string situacao) =>
        ExecutarAsync(
            """
            INSERT INTO autorizacao.versoes_do_perfil
                (id, perfil_id, numero, nome, descricao, protocolo, situacao, origem, expira_em, criada_por_emissor, criada_por_sub, criada_em, created_at)
            VALUES ({0}, {1}, {2}, {3}, 'Descrição de teste.', 'P-1', {4}, 'proposta', now() + interval '15 days', 'emissor', 'diogo.souza', now(), now())
            """,
            Guid.CreateVersion7(), perfilId, numero, Nome(), situacao);

    /// <summary>Apaga as versões pendentes inseridas direto no banco, que não têm documento e quebrariam as leituras das outras suítes.</summary>
    internal async Task LimparPendentesInseridasAsync(Guid perfilId)
    {
        await ExecutarAsync(
            "DELETE FROM autorizacao.decisoes_da_versao WHERE versao_id IN (SELECT id FROM autorizacao.versoes_do_perfil WHERE perfil_id = {0} AND situacao = 'pendente')",
            perfilId);
        await ExecutarAsync("DELETE FROM autorizacao.versoes_do_perfil WHERE perfil_id = {0} AND situacao = 'pendente'", perfilId);
    }

    internal Task InserirVigenciaAsync(Guid perfilId, Guid versaoId) =>
        ExecutarAsync(
            "INSERT INTO autorizacao.vigencias_do_perfil (id, perfil_id, versao_id, desde, created_at) VALUES ({0}, {1}, {2}, now(), now())",
            Guid.CreateVersion7(), perfilId, versaoId);

    internal Task EncerrarVigenciasAsync(Guid perfilId) =>
        ExecutarAsync("UPDATE autorizacao.vigencias_do_perfil SET ate = now() WHERE perfil_id = {0}", perfilId);

    internal Task InserirDocumentoAsync(Envio envio, string situacao) =>
        ExecutarAsync(
            "INSERT INTO autorizacao.documentos_da_versao (id, versao_id, perfil_id, situacao, tamanho_declarado, created_at) VALUES ({0}, {1}, {2}, {3}, 10, now())",
            Guid.CreateVersion7(), envio.VersaoId, envio.PerfilId, situacao);

    internal static async Task<JsonElement[]> ListarTudoAsync(HttpClient client)
    {
        using HttpResponseMessage resposta = await client.GetAsync(new Uri($"{Rota}?limit=100", UriKind.Relative));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument pagina = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement itens = pagina.RootElement.ValueKind == JsonValueKind.Array ? pagina.RootElement : pagina.RootElement.GetProperty("items");

        return [.. itens.EnumerateArray().Select(i => i.Clone())];
    }

    internal async Task SemearConcessaoAsync(Guid perfilId, EscopoDaConcessao escopo)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        IConcessaoRepository repositorio = scope.ServiceProvider.GetRequiredService<IConcessaoRepository>();
        Perfil perfil = (await repositorio.ObterParaConcederAsync(perfilId))!;
        Identidade diogo = new(TestAuthHandler.DefaultIssuer, "diogo.souza");

        Concessao concessao = Concessao.Conceder(
            new Identidade(TestAuthHandler.DefaultIssuer, "leandro.viana"),
            perfil,
            escopo,
            new OrigemDaConcessao(TipoDeOrigem.Pedido, Guid.NewGuid()),
            diogo,
            DateTimeOffset.UtcNow,
            scope.ServiceProvider.GetRequiredService<ICatalogoDePermissoesConsultavel>()).Value!;
        await repositorio.AdicionarAsync(concessao);
        await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().SaveChangesAsync();
    }

    internal static async Task<Guid> CriarAsync(HttpClient client, string[]? permissoes = null)
    {
        using HttpResponseMessage resposta = await EnviarAsync(
            client, HttpMethod.Post, Rota, Corpo(Nome(), permissoes ?? [ConsultarCatalogo]));
        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
        return LerEnvio(await resposta.Content.ReadAsStringAsync()).PerfilId;
    }

    internal static async Task<JsonDocument> LerAsync(HttpClient client, Guid id)
    {
        using HttpResponseMessage resposta = await client.GetAsync(new Uri($"{Rota}/{id}", UriKind.Relative));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
    }

    internal static async Task<HttpResponseMessage> EnviarAsync(
        HttpClient client, HttpMethod metodo, string url, object? corpo = null, string? chave = null)
    {
        using HttpRequestMessage request = new(metodo, new Uri(url, UriKind.Relative));
        if (corpo is not null)
        {
            request.Content = JsonContent.Create(corpo);
        }

        if (metodo != HttpMethod.Get)
        {
            request.Headers.Add("Idempotency-Key", chave ?? Guid.NewGuid().ToString());
        }

        return await client.SendAsync(request);
    }

    // O token só identifica a pessoa; o que ela pode vem da concessão gravada, como em produção.
    internal async Task<HttpClient> ClienteDoGestorAsync()
    {
        await ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, Diogo, TipoDeGovernanca.GestorDeAcessos);
        return Cliente(Diogo);
    }

    /// <summary>Outra pessoa com o perfil de gestor de acessos, para o que a regra proíbe a quem fez o primeiro ato.</summary>
    internal async Task<HttpClient> ClienteDeOutroGestorAsync()
    {
        await ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, OutroGestor, TipoDeGovernanca.GestorDeAcessos);
        return Cliente(OutroGestor);
    }

    internal async Task<HttpClient> ClienteComPermissoesAsync(string sub, params string[] permissoes)
    {
        await ConcessoesDeTeste.ConcederPermissoesAsync(Fixture.Factory.Services, sub, permissoes);
        return Cliente(sub);
    }

    internal HttpClient Cliente(string sub)
    {
        HttpClient client = Fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, sub);
        client.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, sub);
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "teste@unifesspa.edu.br");
        return client;
    }
}
