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

/// <summary>
/// Cadastro de perfis de acesso pelo endpoint real, contra Postgres efêmero. Cobre o que só a
/// borda responde: a permissão exigida em toda operação, o contrato de erro e a proteção dos
/// perfis de governança. As personas são as do portal: Diogo Souza opera o cadastro; Isabella
/// autenticada sem a concessão é recusada.
/// </summary>
[Collection(AutorizacaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class PerfilEndpointTests
{
    private const string Rota = "/api/autorizacao/perfis";
    private const string ConsultarCatalogo = "autorizacao:permissoes:consultar-catalogo";
    private const string ManterMotivos = "selecao:motivos-decisao-isencao:manter";
    private const string OperarInscricoes = "teste:inscricoes:operar";
    private const string AnalisarAtendimento = "teste:atendimento:analisar";
    private const string GestorDeAcessosId = "019a0000-0000-7000-8000-000000000001";

    private const string PermissaoManter = "autorizacao:perfis:manter";
    private const string Diogo = "diogo.souza";

    private readonly AutorizacaoEndpointFixture _fixture;

    public PerfilEndpointTests(AutorizacaoEndpointFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Criar perfil devolve 201 e a leitura traz sensibilidade e permissões do catálogo")]
    public async Task Criar_Valido_LeComSensibilidadeDoCatalogo()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();

        using HttpResponseMessage criada = await EnviarAsync(
            diogo, HttpMethod.Post, Rota, Corpo(Nome(), [ConsultarCatalogo, ManterMotivos]));
        criada.StatusCode.Should().Be(HttpStatusCode.Created, await criada.Content.ReadAsStringAsync());

        using HttpResponseMessage lida = await diogo.GetAsync(criada.Headers.Location);
        lida.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument perfil = JsonDocument.Parse(await lida.Content.ReadAsStringAsync());
        perfil.RootElement.GetProperty("ativo").GetBoolean().Should().BeTrue();
        perfil.RootElement.GetProperty("governanca").ValueKind.Should().Be(JsonValueKind.Null);
        perfil.RootElement.GetProperty("sensibilidade").GetString().Should().Be("interna");
        perfil.RootElement.GetProperty("permissoes").EnumerateArray()
            .Select(p => p.GetProperty("codigo").GetString())
            .Should().Equal(ConsultarCatalogo, ManterMotivos);
    }

    [Fact(DisplayName = "Código fora do catálogo devolve 422 com o código de erro e lista o que não existe")]
    public async Task Criar_PermissaoInexistente_Devolve422()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();

        using HttpResponseMessage resposta = await EnviarAsync(
            diogo, HttpMethod.Post, Rota, Corpo(Nome(), [ConsultarCatalogo, "modulo:que:nao-existe"]));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        string corpo = await resposta.Content.ReadAsStringAsync();
        corpo.Should().Contain("uniplus.autorizacao.perfil.permissao_inexistente").And.Contain("modulo:que:nao-existe");
    }

    [Fact(DisplayName = "Perfil comum com permissão reservada à governança devolve 422")]
    public async Task Criar_PermissaoReservada_Devolve422()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();

        using HttpResponseMessage resposta = await EnviarAsync(
            diogo, HttpMethod.Post, Rota, Corpo(Nome(), [PermissaoManter]));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.permissao_reservada");
    }

    [Fact(DisplayName = "Nome repetido, mesmo com outra caixa, devolve 409")]
    public async Task Criar_NomeRepetidoComOutraCaixa_Devolve409()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        string nome = Nome();

        using HttpResponseMessage primeira = await EnviarAsync(diogo, HttpMethod.Post, Rota, Corpo(nome, [ConsultarCatalogo]));
        primeira.StatusCode.Should().Be(HttpStatusCode.Created);

        using HttpResponseMessage segunda = await EnviarAsync(
            diogo, HttpMethod.Post, Rota, Corpo(nome.ToUpperInvariant(), [ConsultarCatalogo]));

        segunda.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await segunda.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.nome_duplicado");
    }

    [Fact(DisplayName = "PUT com id do caminho diferente do corpo devolve 400")]
    public async Task Atualizar_IdDivergente_Devolve400()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await CriarAsync(diogo);

        using HttpResponseMessage resposta = await EnviarAsync(
            diogo, HttpMethod.Put, $"{Rota}/{id}", CorpoComId(Guid.NewGuid(), Nome(), [ConsultarCatalogo]));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.id_divergente");
    }

    [Fact(DisplayName = "Atualizar troca nome e permissões do perfil comum")]
    public async Task Atualizar_Valido_TrocaOConjunto()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await CriarAsync(diogo);
        string novoNome = Nome();

        using HttpResponseMessage resposta = await EnviarAsync(
            diogo, HttpMethod.Put, $"{Rota}/{id}", CorpoComId(id, novoNome, [ManterMotivos]));
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());

        using JsonDocument perfil = await LerAsync(diogo, id);
        perfil.RootElement.GetProperty("nome").GetString().Should().Be(novoNome);
        perfil.RootElement.GetProperty("permissoes").EnumerateArray()
            .Select(p => p.GetProperty("codigo").GetString()).Should().Equal(ManterMotivos);
    }

    [Fact(DisplayName = "Perfil que mistura permissão de unidade e institucional devolve 422")]
    public async Task Criar_AlcanceMisto_Devolve422()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();

        using HttpResponseMessage resposta = await EnviarAsync(
            diogo, HttpMethod.Post, Rota, Corpo(Nome(), [OperarInscricoes, ManterMotivos]));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.alcance_misto");
    }

    [Fact(DisplayName = "Perfil sem concessões aceita ampliar a sensibilidade e mudar o alcance")]
    public async Task Atualizar_SemConcessoes_AceitaAmpliarEMudarOAlcance()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await CriarAsync(diogo, [OperarInscricoes]);

        using HttpResponseMessage ampliada = await EnviarAsync(
            diogo, HttpMethod.Put, $"{Rota}/{id}", CorpoComId(id, Nome(), [OperarInscricoes, AnalisarAtendimento]));
        using HttpResponseMessage outroAlcance = await EnviarAsync(
            diogo, HttpMethod.Put, $"{Rota}/{id}", CorpoComId(id, Nome(), [ManterMotivos]));

        ampliada.StatusCode.Should().Be(HttpStatusCode.NoContent, await ampliada.Content.ReadAsStringAsync());
        outroAlcance.StatusCode.Should().Be(HttpStatusCode.NoContent, await outroAlcance.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "Perfil com concessão recusa acrescentar permissão sensível e a edição não é gravada")]
    public async Task Atualizar_ComConcessao_AmpliarASensibilidade_Recusa()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await CriarAsync(diogo, [OperarInscricoes]);
        await SemearConcessaoAsync(id, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));

        using HttpResponseMessage resposta = await EnviarAsync(
            diogo, HttpMethod.Put, $"{Rota}/{id}", CorpoComId(id, Nome(), [OperarInscricoes, AnalisarAtendimento]));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.ampliacao_exige_encarregada");
        using JsonDocument perfil = await LerAsync(diogo, id);
        perfil.RootElement.GetProperty("permissoes").EnumerateArray()
            .Select(p => p.GetProperty("codigo").GetString()).Should().Equal(OperarInscricoes);
    }

    [Fact(DisplayName = "Perfil já sensível, com concessão, recusa acrescentar outra permissão sensível")]
    public async Task Atualizar_ComConcessao_OutraSensivelNoPerfilJaSensivel_Recusa()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await CriarAsync(diogo, [AnalisarAtendimento]);
        await SemearConcessaoAsync(id, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));

        using HttpResponseMessage resposta = await EnviarAsync(
            diogo, HttpMethod.Put, $"{Rota}/{id}", CorpoComId(id, Nome(), [AnalisarAtendimento, "teste:laudos:consultar"]));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.ampliacao_exige_encarregada");
    }

    [Fact(DisplayName = "Perfil com concessão recusa mudar de alcance")]
    public async Task Atualizar_ComConcessao_MudarOAlcance_Recusa()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await CriarAsync(diogo, [OperarInscricoes]);
        await SemearConcessaoAsync(id, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));

        using HttpResponseMessage resposta = await EnviarAsync(
            diogo, HttpMethod.Put, $"{Rota}/{id}", CorpoComId(id, Nome(), [ManterMotivos]));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.alcance_alterado");
    }

    [Fact(DisplayName = "Perfil com concessão aceita renomear e manter a sensibilidade")]
    public async Task Atualizar_ComConcessao_SemAmpliar_Aceita()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await CriarAsync(diogo, [OperarInscricoes]);
        await SemearConcessaoAsync(id, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));

        using HttpResponseMessage resposta = await EnviarAsync(
            diogo, HttpMethod.Put, $"{Rota}/{id}", CorpoComId(id, Nome(), [OperarInscricoes]));

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "Desativar e reativar alternam o estado do perfil")]
    public async Task DesativarEReativar_AlternaOEstado()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await CriarAsync(diogo);

        using HttpResponseMessage desativada = await EnviarAsync(diogo, HttpMethod.Delete, $"{Rota}/{id}/ativacao");
        desativada.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LerAsync(diogo, id)).RootElement.GetProperty("ativo").GetBoolean().Should().BeFalse();

        using HttpResponseMessage reativada = await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/ativacao");
        reativada.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LerAsync(diogo, id)).RootElement.GetProperty("ativo").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName = "Perfil de governança recusa edição e desativação com 422")]
    public async Task Governanca_RecusaEdicaoEDesativacao()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = Guid.Parse(GestorDeAcessosId);

        using HttpResponseMessage edicao = await EnviarAsync(
            diogo, HttpMethod.Put, $"{Rota}/{id}", CorpoComId(id, Nome(), [ConsultarCatalogo]));
        using HttpResponseMessage desativacao = await EnviarAsync(diogo, HttpMethod.Delete, $"{Rota}/{id}/ativacao");

        edicao.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        desativacao.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await desativacao.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.governanca_imutavel");
        (await LerAsync(diogo, id)).RootElement.GetProperty("governanca").GetString().Should().Be("gestor-de-acessos");
    }

    [Fact(DisplayName = "Perfil inexistente devolve 404 com o código de erro")]
    public async Task Obter_Inexistente_Devolve404()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();

        using HttpResponseMessage resposta = await diogo.GetAsync(new Uri($"{Rota}/{Guid.NewGuid()}", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.nao_encontrado");
    }

    [Theory(DisplayName = "Sem a permissão de manter perfis, toda operação é 403, inclusive a leitura")]
    [InlineData("GET", "")]
    [InlineData("POST", "")]
    [InlineData("DELETE", "/019a0000-0000-7000-8000-000000000001/ativacao")]
    public async Task SemPermissao_Devolve403(string metodo, string sufixo)
    {
        using HttpClient isabella = await ClienteComPermissoesAsync("leandro.viana", ConsultarCatalogo);

        using HttpResponseMessage resposta = await EnviarAsync(
            isabella, new HttpMethod(metodo), Rota + sufixo, metodo == "POST" ? Corpo(Nome(), [ConsultarCatalogo]) : null);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "Sem autenticação a leitura é 401, não 403")]
    public async Task SemAutenticacao_Devolve401()
    {
        using HttpClient anonimo = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await anonimo.GetAsync(new Uri(Rota, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "A migration cria o schema autorizacao sem chave estrangeira para outro schema")]
    public async Task Migration_NaoCriaFkEntreSchemas()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();

        int fkParaFora = await db.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value"
            FROM pg_constraint c
            JOIN pg_class origem ON origem.oid = c.conrelid
            JOIN pg_namespace no ON no.oid = origem.relnamespace
            JOIN pg_class alvo ON alvo.oid = c.confrelid
            JOIN pg_namespace na ON na.oid = alvo.relnamespace
            WHERE c.contype = 'f' AND no.nspname = 'autorizacao' AND na.nspname <> 'autorizacao'
            """).SingleAsync();
        int tabelas = await db.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value" FROM information_schema.tables
            WHERE table_schema = 'autorizacao' AND table_name IN ('perfis', 'perfil_permissoes')
            """).SingleAsync();

        tabelas.Should().Be(2);
        fkParaFora.Should().Be(0);
    }

    private static string Nome() => $"Perfil {Guid.NewGuid():N}";

    private static object Corpo(string nome, string[] permissoes) =>
        new { nome, descricao = "Perfil de teste do cadastro de acesso.", permissoes };

    private static object CorpoComId(Guid id, string nome, string[] permissoes) =>
        new { id, nome, descricao = "Perfil de teste do cadastro de acesso.", permissoes };

    private async Task SemearConcessaoAsync(Guid perfilId, EscopoDaConcessao escopo)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
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

    private static async Task<Guid> CriarAsync(HttpClient client, string[]? permissoes = null)
    {
        using HttpResponseMessage resposta = await EnviarAsync(
            client, HttpMethod.Post, Rota, Corpo(Nome(), permissoes ?? [ConsultarCatalogo]));
        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
        return Guid.Parse(resposta.Headers.Location!.Segments[^1]);
    }

    private static async Task<JsonDocument> LerAsync(HttpClient client, Guid id)
    {
        using HttpResponseMessage resposta = await client.GetAsync(new Uri($"{Rota}/{id}", UriKind.Relative));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
    }

    private static async Task<HttpResponseMessage> EnviarAsync(
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

    // O token só identifica a pessoa; o que ela pode vem da concessão gravada, como em produção.
    private async Task<HttpClient> ClienteDoGestorAsync()
    {
        await ConcessoesDeTeste.ConcederGovernancaAsync(_fixture.Factory.Services, Diogo, TipoDeGovernanca.GestorDeAcessos);
        return Cliente(Diogo);
    }

    private async Task<HttpClient> ClienteComPermissoesAsync(string sub, params string[] permissoes)
    {
        await ConcessoesDeTeste.ConcederPermissoesAsync(_fixture.Factory.Services, sub, permissoes);
        return Cliente(sub);
    }

    private HttpClient Cliente(string sub)
    {
        HttpClient client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, sub);
        client.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, sub);
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "teste@unifesspa.edu.br");
        return client;
    }
}
