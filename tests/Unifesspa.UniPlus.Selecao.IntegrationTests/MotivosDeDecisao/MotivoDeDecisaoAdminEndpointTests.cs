namespace Unifesspa.UniPlus.Selecao.IntegrationTests.MotivosDeDecisao;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Outbox.Cascading;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Manutenção do cadastro único de motivos de decisão pelo endpoint real,
/// contra Postgres via Testcontainers. Cobre os critérios de aceite da Task
/// #1229 que só a borda responde: a escrita exige a permissão pelo ponto de
/// decisão único, e quem não a tem é recusado ainda que traga outro papel
/// administrativo.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class MotivoDeDecisaoAdminEndpointTests : IAsyncLifetime
{
    private static readonly string PermissaoManter = PermissaoSelecao.MotivosDeDecisaoManter.Codigo;
    private const string AdminPlataforma = "plataforma-admin";
    private const string Leandro = "leandro.viana";
    private const string Diogo = "diogo.souza";
    private const string Paulo = "paulo.conceicao";
    private const string Lucia = "lucia.almeida";
    private const string PapelDeInstalacao = "uniplus-instalacao";
    private const string PrefixoDeTeste = "TEST_MDI_";

    private readonly CascadingFixture _fixture;

    public MotivoDeDecisaoAdminEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    // As personas compartilham o banco da coleção: cada teste começa sem concessão alguma.
    public async Task InitializeAsync()
    {
        foreach (string sub in new[] { Leandro, Diogo, Paulo, Lucia })
        {
            await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, sub);
        }
    }

    /// <summary>
    /// O Postgres é compartilhado por toda a coleção. Os motivos que esta
    /// classe cria são identificados pelo prefixo do código e apagados ao fim,
    /// para não vazarem para a próxima classe.
    /// </summary>
    public async Task DisposeAsync()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();

        await db.Database.ExecuteSqlAsync(
            $"DELETE FROM selecao.motivos_de_decisao WHERE codigo LIKE 'TEST\\_MDI\\_%' ESCAPE '\\'");
    }

    [Fact(DisplayName = "POST com a permissão de manutenção cria o motivo")]
    public async Task Criar_ComPermissao_Cria()
    {
        using HttpClient client = await ClienteComConcessaoAsync();

        using HttpResponseMessage resposta = await PostAsync(
            client, "/api/selecao/admin/motivos-de-decisao", PayloadValido(Codigo()));

        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "POST sem a permissão é recusado, ainda que o usuário seja admin da plataforma")]
    public async Task Criar_SemPermissao_Recusa()
    {
        // O papel de realm mais amplo da plataforma não concede a permissão: a
        // autorização é pela concessão, não pelo nome do perfil.
        using HttpClient client = ClienteComPapeisDeRealm(Diogo, AdminPlataforma);
        string codigo = Codigo();

        using HttpResponseMessage resposta = await PostAsync(
            client, "/api/selecao/admin/motivos-de-decisao", PayloadValido(codigo));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ExisteNoBanco(codigo)).Should().BeFalse("a recusa precede a escrita");
    }

    [Fact(DisplayName = "POST com o papel da própria permissão no token, sem concessão gravada, é recusado")]
    public async Task Criar_ComPapelDaPermissaoSemConcessao_Recusa()
    {
        using HttpClient client = ClienteComPapeisDeRealm(Leandro, PermissaoManter);
        string codigo = Codigo();

        using HttpResponseMessage resposta = await PostAsync(
            client, "/api/selecao/admin/motivos-de-decisao", PayloadValido(codigo));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ExisteNoBanco(codigo)).Should().BeFalse("a recusa precede a escrita");
        (await resposta.Content.ReadAsStringAsync()).Should().NotContain(Leandro, "a recusa não expõe quem foi recusado");
    }

    [Fact(DisplayName = "POST com o papel de instalação e sem concessão é recusado: o papel só instala")]
    public async Task Criar_ComPapelDeInstalacaoSemConcessao_Recusa()
    {
        using HttpClient client = ClienteComPapeisDeRealm(Paulo, PapelDeInstalacao);

        using HttpResponseMessage resposta = await PostAsync(
            client, "/api/selecao/admin/motivos-de-decisao", PayloadValido(Codigo()));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "A revogação da concessão vale na requisição seguinte")]
    public async Task Criar_AposRevogacaoDaConcessao_Recusa()
    {
        Guid concessao = await ConcessoesDeTeste.ConcederPermissoesAsync(_fixture.Factory.Services, Leandro, PermissaoManter);
        using HttpClient leandro = ClienteAutenticado(Leandro);
        using HttpResponseMessage antes = await PostAsync(
            leandro, "/api/selecao/admin/motivos-de-decisao", PayloadValido(Codigo()));
        antes.StatusCode.Should().Be(HttpStatusCode.Created, await antes.Content.ReadAsStringAsync());

        await ConcessoesDeTeste.ConcederGovernancaAsync(_fixture.Factory.Services, Diogo, TipoDeGovernanca.GestorDeAcessos);
        using HttpClient gestor = ClienteAutenticado(Diogo);
        using HttpResponseMessage revogada = await PostAsync(
            gestor, $"/api/autorizacao/concessoes/{concessao}/revogacao", new { motivo = "Mudou de função na unidade.", confirmacaoEmail = ConcessoesDeTeste.EmailDeTeste });
        revogada.StatusCode.Should().Be(HttpStatusCode.NoContent, await revogada.Content.ReadAsStringAsync());

        using HttpResponseMessage depois = await PostAsync(
            leandro, "/api/selecao/admin/motivos-de-decisao", PayloadValido(Codigo()));

        depois.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "Sem a permissão de manutenção, editar, desativar e reativar motivo são recusados")]
    public async Task ManterSemPermissao_Recusa()
    {
        using HttpClient comPermissao = await ClienteComConcessaoAsync();
        using HttpResponseMessage criada = await PostAsync(comPermissao, "/api/selecao/admin/motivos-de-decisao", PayloadValido(Codigo()));
        Guid id = await criada.Content.ReadFromJsonAsync<Guid>();
        using HttpClient lucia = ClienteAutenticado(Lucia);

        using HttpResponseMessage edicao = await EnviarAsync(lucia, HttpMethod.Put, $"/api/selecao/admin/motivos-de-decisao/{id}", new { id, descricao = "Outra redação do motivo." });
        using HttpResponseMessage desativacao = await EnviarAsync(lucia, HttpMethod.Delete, $"/api/selecao/admin/motivos-de-decisao/{id}/ativacao", null);
        using HttpResponseMessage reativacao = await EnviarAsync(lucia, HttpMethod.Post, $"/api/selecao/admin/motivos-de-decisao/{id}/ativacao", null);

        edicao.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        desativacao.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        reativacao.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "POST sem autenticação alguma é 401, não 403")]
    public async Task Criar_SemAutenticacao_NaoAutorizado()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await PostAsync(
            client, "/api/selecao/admin/motivos-de-decisao", PayloadValido(Codigo()));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "responder 403 diria que a identidade foi lida e recusada, quando não houve identidade");
    }

    [Fact(DisplayName = "POST sem sentido é recusado com 422")]
    public async Task Criar_SemSentido_Recusa()
    {
        using HttpClient client = await ClienteComConcessaoAsync();

        using HttpResponseMessage resposta = await PostAsync(
            client,
            "/api/selecao/admin/motivos-de-decisao",
            new
            {
                tipo = "ISENCAO_DA_TAXA",
                codigo = Codigo(),
                descricao = "Renda familiar per capita acima do limite legal.",
                subclassificacao = "CADASTRO_UNICO",
                sentido = (string?)null,
            });

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "POST repetido com o mesmo código devolve conflito")]
    public async Task Criar_CodigoRepetido_Conflito()
    {
        using HttpClient client = await ClienteComConcessaoAsync();
        string codigo = Codigo();

        using HttpResponseMessage primeira = await PostAsync(
            client, "/api/selecao/admin/motivos-de-decisao", PayloadValido(codigo));
        primeira.StatusCode.Should().Be(HttpStatusCode.Created);

        using HttpResponseMessage segunda = await PostAsync(
            client, "/api/selecao/admin/motivos-de-decisao", PayloadValido(codigo));

        segunda.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "Desativar retira o motivo da listagem padrão sem apagar o registro")]
    public async Task Desativar_MotivoAtivo_SaiDaListagemEPermanece()
    {
        using HttpClient client = await ClienteComConcessaoAsync();
        string codigo = Codigo();

        using HttpResponseMessage criada = await PostAsync(
            client, "/api/selecao/admin/motivos-de-decisao", PayloadValido(codigo));
        Guid id = await criada.Content.ReadFromJsonAsync<Guid>();

        using HttpRequestMessage desativar = new(
            HttpMethod.Delete,
            new Uri($"/api/selecao/admin/motivos-de-decisao/{id}/ativacao", UriKind.Relative));
        desativar.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using HttpResponseMessage respostaDesativacao = await client.SendAsync(desativar);

        respostaDesativacao.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ExisteNoBanco(codigo)).Should().BeTrue(
            "a retirada do catálogo é a desativação, e o registro segue legível para quem já o referencia");

        using HttpResponseMessage consulta = await client.GetAsync(
            new Uri($"/api/selecao/motivos-de-decisao/{id}", UriKind.Relative));
        consulta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "O mesmo código em outro tipo é aceito: o código é único por tipo")]
    public async Task Criar_MesmoCodigoEmOutroTipo_Aceita()
    {
        using HttpClient client = await ClienteComConcessaoAsync();
        string codigo = Codigo();

        using HttpResponseMessage isencao = await PostAsync(client, "/api/selecao/admin/motivos-de-decisao", PayloadValido(codigo));
        using HttpResponseMessage homologacao = await PostAsync(client, "/api/selecao/admin/motivos-de-decisao", PayloadDaHomologacao(codigo));

        isencao.StatusCode.Should().Be(HttpStatusCode.Created);
        homologacao.StatusCode.Should().Be(HttpStatusCode.Created, await homologacao.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "POST com subclassificação em tipo que não a define é recusado com o código nomeado")]
    public async Task Criar_SubclassificacaoForaDaIsencao_Recusa()
    {
        using HttpClient client = await ClienteComConcessaoAsync();

        using HttpResponseMessage resposta = await PostAsync(
            client, "/api/selecao/admin/motivos-de-decisao", PayloadDaHomologacao(Codigo(), "CADASTRO_UNICO"));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("uniplus.selecao.motivo_de_decisao.subclassificacao_nao_admitida");
    }

    [Fact(DisplayName = "O filtro por tipo e subclassificação aceita os códigos canônicos que a leitura devolve")]
    public async Task Listar_FiltroPeloCodigoCanonico_Filtra()
    {
        using HttpClient client = await ClienteComConcessaoAsync();
        string codigo = Codigo();

        using HttpResponseMessage criada = await PostAsync(
            client, "/api/selecao/admin/motivos-de-decisao", PayloadValido(codigo));
        criada.StatusCode.Should().Be(HttpStatusCode.Created);

        // O DTO devolve tipo e subclassificação como código canônico; devolver esses mesmos
        // valores no filtro tem de funcionar.
        using HttpResponseMessage resposta = await client.GetAsync(
            new Uri("/api/selecao/motivos-de-decisao?tipo=ISENCAO_DA_TAXA&subclassificacao=CADASTRO_UNICO&limit=100", UriKind.Relative));

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        corpo.Should().Contain(codigo);
        using HttpResponseMessage outroTipo = await client.GetAsync(
            new Uri("/api/selecao/motivos-de-decisao?tipo=CLASSIFICACAO&limit=100", UriKind.Relative));
        (await outroTipo.Content.ReadAsStringAsync()).Should().NotContain(codigo);
    }

    [Fact(DisplayName = "O filtro recusa tipo fora do vocabulário canônico")]
    public async Task Listar_FiltroForaDoVocabulario_Recusa()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await client.GetAsync(
            new Uri("/api/selecao/motivos-de-decisao?tipo=NAO_EXISTE", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "A sentinela de tipo não informado não é aceita como filtro")]
    public async Task Listar_FiltroComSentinela_Recusa()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        // Nenhum é sentinela de ausência, não um tipo — aceitá-la como filtro
        // devolveria uma lista sempre vazia.
        using HttpResponseMessage resposta = await client.GetAsync(
            new Uri("/api/selecao/motivos-de-decisao?tipo=NENHUM", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "Descrição com caractere nulo é recusada pelo domínio, não pelo banco")]
    public async Task Criar_DescricaoComCaractereNulo_Recusa()
    {
        using HttpClient client = await ClienteComConcessaoAsync();

        using HttpResponseMessage resposta = await PostAsync(
            client,
            "/api/selecao/admin/motivos-de-decisao",
            new
            {
                tipo = "ISENCAO_DA_TAXA",
                codigo = Codigo(),
                descricao = "Renda\u0000acima do limite.",
                subclassificacao = "CADASTRO_UNICO",
                sentido = "DESFAVORAVEL",
            });

        // O Postgres não armazena U+0000 em coluna textual. Sem recusa no
        // agregado, o valor atravessa a validação e estoura no SaveChanges,
        // devolvendo 500 para um payload que é apenas inválido.
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "Token sem jti responde o 401 canônico da API, com desafio e problem+json")]
    public async Task Criar_TokenSemJti_RespondeDesafioCanonico()
    {
        // Token que a autenticação aceita mas do qual o sujeito da decisão não
        // se monta: a borda responde identidade incompleta. O 401 daí precisa
        // ser o mesmo de qualquer outra falha de autenticação desta API, senão
        // o cliente não reconhece que deve renovar a credencial.
        HttpClient client = await ClienteComConcessaoAsync();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SemJtiHeader, "1");

        using (client)
        {
            using HttpResponseMessage resposta = await PostAsync(
                client, "/api/selecao/admin/motivos-de-decisao", PayloadValido(Codigo()));

            resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            resposta.Headers.WwwAuthenticate.Should().NotBeEmpty(
                "o desafio é o que diz ao cliente que a credencial precisa ser renovada");
            resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        }
    }

    private static object PayloadValido(string codigo) => new
    {
        tipo = "ISENCAO_DA_TAXA",
        codigo,
        descricao = "Renda familiar per capita acima do limite legal.",
        subclassificacao = "CADASTRO_UNICO",
        sentido = "DESFAVORAVEL",
    };

    private static object PayloadDaHomologacao(string codigo, string? subclassificacao = null) => new
    {
        tipo = "HOMOLOGACAO_DAS_INSCRICOES",
        codigo,
        descricao = "Sem resultado no retorno do INEP para a edição indicada",
        subclassificacao,
        sentido = "DESFAVORAVEL",
    };

    private static string Codigo() => $"{PrefixoDeTeste}{Guid.NewGuid():N}"[..40].ToUpperInvariant();

    private async Task<bool> ExisteNoBanco(string codigo)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();

        CodigoMotivoDecisao codigoVo = CodigoMotivoDecisao.Criar(codigo).Value!;

        return await db.MotivosDeDecisao.AsNoTracking()
            .AnyAsync(motivo => motivo.Codigo == codigoVo);
    }

    // O token só identifica Leandro; quem lhe dá a permissão é a concessão gravada do perfil.
    private async Task<HttpClient> ClienteComConcessaoAsync()
    {
        await ConcessoesDeTeste.ConcederPermissoesAsync(_fixture.Factory.Services, Leandro, PermissaoManter);
        return ClienteAutenticado(Leandro);
    }

    private HttpClient ClienteComPapeisDeRealm(string sub, params string[] papeis)
    {
        HttpClient client = ClienteAutenticado(sub);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', papeis));
        return client;
    }

    private HttpClient ClienteAutenticado(string sub)
    {
        HttpClient client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme,
            TestAuthHandler.TokenValue);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, sub);
        client.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, "Admin Test");
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "admin@unifesspa.edu.br");
        return client;
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, HttpMethod metodo, string url, object? payload)
    {
        using HttpRequestMessage request = new(metodo, new Uri(url, UriKind.Relative));
        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, object payload)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(url, UriKind.Relative))
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }
}
