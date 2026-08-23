namespace Unifesspa.UniPlus.Selecao.IntegrationTests.MotivosDecisaoIsencao;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Outbox.Cascading;

using Unifesspa.UniPlus.Authorization;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Manutenção do catálogo de motivos de decisão de isenção pelo endpoint real,
/// contra Postgres via Testcontainers. Cobre os critérios de aceite da Task
/// #1229 que só a borda responde: a escrita exige a permissão pelo ponto de
/// decisão único, e quem não a tem é recusado ainda que traga outro papel
/// administrativo.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class MotivoDecisaoIsencaoAdminEndpointTests : IAsyncLifetime
{
    private const string PermissaoManter = UniPlusPermissions.ConfiguracaoMotivosDecisaoRecursalManter;
    private const string AdminPlataforma = "plataforma-admin";
    private const string PrefixoDeTeste = "TEST_MDI_";

    private readonly CascadingFixture _fixture;

    public MotivoDecisaoIsencaoAdminEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

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
            $"DELETE FROM selecao.motivos_decisao_isencao WHERE codigo LIKE 'TEST\\_MDI\\_%' ESCAPE '\\'");
    }

    [Fact(DisplayName = "POST com a permissão de manutenção cria o motivo")]
    public async Task Criar_ComPermissao_Cria()
    {
        using HttpClient client = ClienteComPermissoes(PermissaoManter);

        using HttpResponseMessage resposta = await PostAsync(
            client, "/api/selecao/admin/motivos-decisao-isencao", PayloadValido(Codigo()));

        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "POST sem a permissão é recusado, ainda que o usuário seja admin da plataforma")]
    public async Task Criar_SemPermissao_Recusa()
    {
        // O papel de realm mais amplo da plataforma não concede a permissão: a
        // autorização é pela concessão, não pelo nome do perfil.
        using HttpClient client = ClienteComPapeisDeRealm(AdminPlataforma);
        string codigo = Codigo();

        using HttpResponseMessage resposta = await PostAsync(
            client, "/api/selecao/admin/motivos-decisao-isencao", PayloadValido(codigo));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ExisteNoBanco(codigo)).Should().BeFalse("a recusa precede a escrita");
    }

    [Fact(DisplayName = "POST sem autenticação alguma é 401, não 403")]
    public async Task Criar_SemAutenticacao_NaoAutorizado()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await PostAsync(
            client, "/api/selecao/admin/motivos-decisao-isencao", PayloadValido(Codigo()));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "responder 403 diria que a identidade foi lida e recusada, quando não houve identidade");
    }

    [Fact(DisplayName = "POST sem resultado permitido é recusado com 422")]
    public async Task Criar_SemResultadoPermitido_Recusa()
    {
        using HttpClient client = ClienteComPermissoes(PermissaoManter);

        using HttpResponseMessage resposta = await PostAsync(
            client,
            "/api/selecao/admin/motivos-decisao-isencao",
            new
            {
                codigo = Codigo(),
                descricao = "Renda familiar per capita acima do limite legal.",
                fundamento = "CADASTRO_UNICO",
                resultadoPermitido = (string?)null,
            });

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "POST repetido com o mesmo código devolve conflito")]
    public async Task Criar_CodigoRepetido_Conflito()
    {
        using HttpClient client = ClienteComPermissoes(PermissaoManter);
        string codigo = Codigo();

        using HttpResponseMessage primeira = await PostAsync(
            client, "/api/selecao/admin/motivos-decisao-isencao", PayloadValido(codigo));
        primeira.StatusCode.Should().Be(HttpStatusCode.Created);

        using HttpResponseMessage segunda = await PostAsync(
            client, "/api/selecao/admin/motivos-decisao-isencao", PayloadValido(codigo));

        segunda.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "Desativar retira o motivo da listagem padrão sem apagar o registro")]
    public async Task Desativar_MotivoAtivo_SaiDaListagemEPermanece()
    {
        using HttpClient client = ClienteComPermissoes(PermissaoManter);
        string codigo = Codigo();

        using HttpResponseMessage criada = await PostAsync(
            client, "/api/selecao/admin/motivos-decisao-isencao", PayloadValido(codigo));
        Guid id = await criada.Content.ReadFromJsonAsync<Guid>();

        using HttpRequestMessage desativar = new(
            HttpMethod.Delete,
            new Uri($"/api/selecao/admin/motivos-decisao-isencao/{id}/ativacao", UriKind.Relative));
        desativar.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using HttpResponseMessage respostaDesativacao = await client.SendAsync(desativar);

        respostaDesativacao.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ExisteNoBanco(codigo)).Should().BeTrue(
            "a retirada do catálogo é a desativação, e o registro segue legível para quem já o referencia");

        using HttpResponseMessage consulta = await client.GetAsync(
            new Uri($"/api/selecao/motivos-decisao-isencao/{id}", UriKind.Relative));
        consulta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "O filtro por fundamento aceita o mesmo código canônico que a leitura devolve")]
    public async Task Listar_FiltroPeloCodigoCanonico_Filtra()
    {
        using HttpClient client = ClienteComPermissoes(PermissaoManter);
        string codigo = Codigo();

        using HttpResponseMessage criada = await PostAsync(
            client, "/api/selecao/admin/motivos-decisao-isencao", PayloadValido(codigo));
        criada.StatusCode.Should().Be(HttpStatusCode.Created);

        // O DTO devolve fundamento como "CADASTRO_UNICO"; devolver esse mesmo
        // valor no filtro tem de funcionar. Ligar o parâmetro direto ao enum
        // faria a API recusar o que ela própria emitiu.
        using HttpResponseMessage resposta = await client.GetAsync(
            new Uri("/api/selecao/motivos-decisao-isencao?fundamento=CADASTRO_UNICO", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "O filtro recusa fundamento fora do vocabulário canônico")]
    public async Task Listar_FiltroForaDoVocabulario_Recusa()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await client.GetAsync(
            new Uri("/api/selecao/motivos-decisao-isencao?fundamento=NAO_EXISTE", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "A sentinela de fundamento não informado não é aceita como filtro")]
    public async Task Listar_FiltroComSentinela_Recusa()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        // Nenhum é sentinela de ausência, não um fundamento — aceitá-la como
        // filtro devolveria a lista dos motivos que não têm fundamento, que por
        // invariante é sempre vazia.
        using HttpResponseMessage resposta = await client.GetAsync(
            new Uri("/api/selecao/motivos-decisao-isencao?fundamento=NENHUM", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "Descrição com caractere nulo é recusada pelo domínio, não pelo banco")]
    public async Task Criar_DescricaoComCaractereNulo_Recusa()
    {
        using HttpClient client = ClienteComPermissoes(PermissaoManter);

        using HttpResponseMessage resposta = await PostAsync(
            client,
            "/api/selecao/admin/motivos-decisao-isencao",
            new
            {
                codigo = Codigo(),
                descricao = "Renda\u0000acima do limite.",
                fundamento = "CADASTRO_UNICO",
                resultadoPermitido = "INDEFERIDO",
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
        HttpClient client = ClienteAutenticado();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SemJtiHeader, "1");
        client.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, PermissaoManter);

        using (client)
        {
            using HttpResponseMessage resposta = await PostAsync(
                client, "/api/selecao/admin/motivos-decisao-isencao", PayloadValido(Codigo()));

            resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            resposta.Headers.WwwAuthenticate.Should().NotBeEmpty(
                "o desafio é o que diz ao cliente que a credencial precisa ser renovada");
            resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        }
    }

    private static object PayloadValido(string codigo) => new
    {
        codigo,
        descricao = "Renda familiar per capita acima do limite legal.",
        fundamento = "CADASTRO_UNICO",
        resultadoPermitido = "INDEFERIDO",
    };

    private static string Codigo() => $"{PrefixoDeTeste}{Guid.NewGuid():N}"[..40].ToUpperInvariant();

    private async Task<bool> ExisteNoBanco(string codigo)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();

        CodigoMotivoDecisao codigoVo = CodigoMotivoDecisao.Criar(codigo).Value!;

        return await db.MotivosDecisaoIsencao.AsNoTracking()
            .AnyAsync(motivo => motivo.Codigo == codigoVo);
    }

    private HttpClient ClienteComPermissoes(params string[] permissoes)
    {
        HttpClient client = ClienteAutenticado();
        client.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, string.Join(',', permissoes));
        return client;
    }

    private HttpClient ClienteComPapeisDeRealm(params string[] papeis)
    {
        HttpClient client = ClienteAutenticado();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', papeis));
        return client;
    }

    private HttpClient ClienteAutenticado()
    {
        HttpClient client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme,
            TestAuthHandler.TokenValue);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, $"admin-{Guid.NewGuid():N}"[..16]);
        client.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, "Admin Test");
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "admin@unifesspa.edu.br");
        return client;
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
