namespace Unifesspa.UniPlus.Selecao.IntegrationTests.SpikeIdempotencia;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Outbox.Cascading;

using Unifesspa.UniPlus.Authorization;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// SPIKE — nao mergear. Cada teste responde uma pergunta em aberto sobre a
/// politica de armazenamento do cache de idempotencia, inspecionando a linha
/// gravada em idempotency_cache alem do status devolvido ao cliente.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class SpikeIdempotenciaTests
{
    private const string AdminPlataforma = "plataforma-admin";
    private const string PermissaoManterMotivos = UniPlusPermissions.ConfiguracaoMotivosDecisaoRecursalManter;

    /// Discriminator polimorfico do PredicadoObrigatoriedade (ADR-0058).
    private static readonly Dictionary<string, object> PredicadoValido = new(StringComparer.Ordinal)
    {
        ["$tipo"] = "concorrenciaDuplaObrigatoria",
    };

    /// Discriminator inexistente — nao desserializa.
    private static readonly Dictionary<string, object> PredicadoMalformado = new(StringComparer.Ordinal)
    {
        ["tipo"] = "sempre",
    };

    private readonly CascadingFixture _fixture;

    public SpikeIdempotenciaTests(CascadingFixture fixture) => _fixture = fixture;

    // ---------------------------------------------------------------
    // P1 — um 422 de FluentValidation tranca a chave?
    // ValidationException sobe ate o GlobalExceptionMiddleware, FORA do MVC,
    // e chega ao filtro como executed.Exception pendente.
    // ---------------------------------------------------------------
    [Fact(DisplayName = "P1: 422 de validacao — o que fica no store e o que o retry recebe")]
    public async Task P1_ValidacaoFluentValidation()
    {
        string userId = $"spike-{Guid.NewGuid():N}"[..16];
        using HttpClient client = ClienteComPapeis(userId, AdminPlataforma);
        string key = Guid.NewGuid().ToString();
        const string Url = "/api/selecao/admin/obrigatoriedades-legais";

        // tipoProcessoCodigo vazio viola o NotEmpty do validator.
        object payloadInvalido = new
        {
            tipoProcessoCodigo = "",
            categoria = "outros",
            regraCodigo = $"SPIKE_{Guid.NewGuid():N}"[..30],
            predicado = PredicadoValido,
            descricaoHumana = "Spike de validacao",
            baseLegal = "Lei",
            vigenciaInicio = "2026-01-01",
        };

        using HttpResponseMessage primeira = await PostAsync(client, Url, payloadInvalido, key);
        string corpo1 = await primeira.Content.ReadAsStringAsync();

        IdempotencyEntry? entrada = await LerEntradaAsync(userId, $"POST {Url.ToLowerInvariant()}", key);

        using HttpResponseMessage retry = await PostAsync(client, Url, payloadInvalido, key);
        string corpo2 = await retry.Content.ReadAsStringAsync();

        throw new SpikeResultado($"""
            P1 — 422 de FluentValidation
              1a resposta ....... {(int)primeira.StatusCode}
              corpo ............. {Truncar(corpo1)}
              entrada no store .. {Descrever(entrada)}
              retry (mesma key) . {(int)retry.StatusCode}
              corpo retry ....... {Truncar(corpo2)}
            """);
    }

    // ---------------------------------------------------------------
    // P2 — o 403 emitido DE DENTRO da action e replayado depois da concessao?
    // E a issue #1262. Mesmo usuario, mesma chave, permissao concedida entre
    // as duas chamadas.
    // ---------------------------------------------------------------
    [Fact(DisplayName = "P2: 403 de dentro da action — replay apos a permissao ser concedida")]
    public async Task P2_ForbiddenReplayado()
    {
        string userId = $"spike-{Guid.NewGuid():N}"[..16];
        string key = Guid.NewGuid().ToString();
        const string Url = "/api/selecao/admin/motivos-decisao-isencao";
        string codigo = $"TEST_MDI_{Guid.NewGuid():N}"[..30].ToUpperInvariant();

        object payload = new
        {
            codigo,
            descricao = "Renda familiar per capita acima do limite legal.",
            fundamento = "CADASTRO_UNICO",
            resultadoPermitido = "INDEFERIDO",
        };

        // 1a: mesmo usuario, SEM a permissao de manutencao.
        using HttpClient semPermissao = ClienteComPapeis(userId, AdminPlataforma);
        using HttpResponseMessage negada = await PostAsync(semPermissao, Url, payload, key);
        string corpoNegado = await negada.Content.ReadAsStringAsync();

        IdempotencyEntry? entrada = await LerEntradaAsync(userId, $"POST {Url.ToLowerInvariant()}", key);

        // 2a: MESMO usuario, MESMA chave, agora COM a permissao.
        using HttpClient comPermissao = ClienteComPermissoes(userId, PermissaoManterMotivos);
        using HttpResponseMessage aposConcessao = await PostAsync(comPermissao, Url, payload, key);
        string corpoApos = await aposConcessao.Content.ReadAsStringAsync();

        bool criou = await ExisteMotivoAsync(codigo);

        throw new SpikeResultado($"""
            P2 — 403 de dentro da action (issue #1262)
              1a resposta (sem permissao) .. {(int)negada.StatusCode}
              entrada no store ............. {Descrever(entrada)}
              2a resposta (COM permissao) .. {(int)aposConcessao.StatusCode}
              corpo da 2a .................. {Truncar(corpoApos)}
              motivo criado no banco ....... {criou}
            """);
    }

    // ---------------------------------------------------------------
    // P3 — o replay preserva WWW-Authenticate? SerializeCachedHeaders
    // persiste apenas Content-Type, Location e ETag.
    // ---------------------------------------------------------------
    [Fact(DisplayName = "P3: headers preservados no replay de uma recusa")]
    public async Task P3_HeadersNoReplay()
    {
        string userId = $"spike-{Guid.NewGuid():N}"[..16];
        string key = Guid.NewGuid().ToString();
        const string Url = "/api/selecao/admin/motivos-decisao-isencao";

        object payload = new
        {
            codigo = $"TEST_MDI_{Guid.NewGuid():N}"[..30].ToUpperInvariant(),
            descricao = "Renda familiar per capita acima do limite legal.",
            fundamento = "CADASTRO_UNICO",
            resultadoPermitido = "INDEFERIDO",
        };

        using HttpClient client = ClienteComPapeis(userId, AdminPlataforma);
        using HttpResponseMessage primeira = await PostAsync(client, Url, payload, key);
        using HttpResponseMessage replay = await PostAsync(client, Url, payload, key);

        throw new SpikeResultado($"""
            P3 — headers no replay
              1a  status ......... {(int)primeira.StatusCode}
              1a  headers ........ {Headers(primeira)}
              2a  status ......... {(int)replay.StatusCode}
              2a  headers ........ {Headers(replay)}
              Idempotency-Replayed 2a: {(replay.Headers.TryGetValues("Idempotency-Replayed", out IEnumerable<string>? v) ? string.Join(",", v) : "(ausente)")}
            """);
    }

    // ---------------------------------------------------------------
    // P4 — corpo que nao desserializa (discriminator polimorfico invalido):
    // qual status sai, e a chave fica trancada?
    // ---------------------------------------------------------------
    [Fact(DisplayName = "P4: corpo que nao desserializa — status e estado da chave")]
    public async Task P4_CorpoQueNaoDesserializa()
    {
        string userId = $"spike-{Guid.NewGuid():N}"[..16];
        using HttpClient client = ClienteComPapeis(userId, AdminPlataforma);
        string key = Guid.NewGuid().ToString();
        const string Url = "/api/selecao/admin/obrigatoriedades-legais";

        object payload = new
        {
            tipoProcessoCodigo = "*",
            categoria = "outros",
            regraCodigo = $"SPIKE_{Guid.NewGuid():N}"[..30],
            predicado = PredicadoMalformado,
            descricaoHumana = "Spike de desserializacao",
            baseLegal = "Lei",
            vigenciaInicio = "2026-01-01",
        };

        using HttpResponseMessage primeira = await PostAsync(client, Url, payload, key);
        string corpo1 = await primeira.Content.ReadAsStringAsync();

        IdempotencyEntry? entrada = await LerEntradaAsync(userId, $"POST {Url.ToLowerInvariant()}", key);

        using HttpResponseMessage retry = await PostAsync(client, Url, payload, key);
        string corpo2 = await retry.Content.ReadAsStringAsync();

        throw new SpikeResultado($"""
            P4 — corpo que nao desserializa
              1a resposta ....... {(int)primeira.StatusCode}
              corpo ............. {Truncar(corpo1)}
              entrada no store .. {Descrever(entrada)}
              retry (mesma key) . {(int)retry.StatusCode}
              corpo retry ....... {Truncar(corpo2)}
            """);
    }

    // ---------------------------------------------------------------
    // P5 — o catch de violacao de constraint que NAO chama
    // DescartarAlteracoesNaoSalvas devolve 409 ou 500?
    // So a CORRIDA chega ao catch: o caminho sequencial e barrado antes,
    // pelo check-then-act. Requests concorrentes com o mesmo regraCodigo.
    // ---------------------------------------------------------------
    [Fact(DisplayName = "P5: corrida em constraint unica — 409 pretendido ou 500?")]
    public async Task P5_CorridaEmConstraint()
    {
        const string Url = "/api/selecao/admin/obrigatoriedades-legais";
        const int Paralelas = 8;
        const int Rodadas = 6;

        List<string> observado = [];

        for (int rodada = 0; rodada < Rodadas; rodada++)
        {
            string regraCodigo = $"SPIKE_{Guid.NewGuid():N}"[..30];
            object payload = new
            {
                tipoProcessoCodigo = "*",
                categoria = "outros",
                regraCodigo,
                predicado = PredicadoValido,
                descricaoHumana = $"Spike corrida {rodada}",
                baseLegal = "Lei",
                vigenciaInicio = "2026-01-01",
            };

            IEnumerable<Task<int>> disparos = Enumerable.Range(0, Paralelas).Select(async _ =>
            {
                string userId = $"spike-{Guid.NewGuid():N}"[..16];
                using HttpClient c = ClienteComPapeis(userId, AdminPlataforma);
                using HttpResponseMessage r = await PostAsync(c, Url, payload, Guid.NewGuid().ToString());
                return (int)r.StatusCode;
            });

            int[] status = await Task.WhenAll(disparos);
            observado.Add($"rodada {rodada}: {string.Join(" ", status.OrderBy(x => x))}");
        }

        throw new SpikeResultado($"""
            P5 — corrida em constraint unica (catch sem DescartarAlteracoesNaoSalvas)
            {string.Join(Environment.NewLine, observado.Select(l => "  " + l))}
              esperado do desenho: 201 uma vez, 409 nas demais
              500 em qualquer posicao = a excecao repetiu fora do catch
            """);
    }

    // ---------------------------------------------------------------
    // P6 — mecanica da ADR-0119, isolada do acaso da corrida: um catch que
    // devolve Failure SEM limpar o ChangeTracker deixa a entidade Added.
    // O SaveChangesAsync que o AutoApplyTransactions dispara depois do
    // handler encontra o mesmo estado — repete a excecao (500) ou nao?
    // ---------------------------------------------------------------
    [Fact(DisplayName = "P6: segundo SaveChanges com tracker sujo repete a excecao?")]
    public async Task P6_SegundoSaveComTrackerSujo()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();

        string codigo = $"TEST_MDI_{Guid.NewGuid():N}"[..30].ToUpperInvariant();

        // Semeia a linha com que a insercao seguinte vai colidir.
        MotivoDecisaoIsencao semeado = MotivoDecisaoIsencao.Criar(
            codigo, "Semeado pelo spike", FundamentoIsencao.CadastroUnico, ResultadoPermitido.Indeferido).Value!;
        db.MotivosDecisaoIsencao.Add(semeado);
        await db.SaveChangesAsync();

        // Mesma situacao do handler: entidade colidente entra no tracker e o
        // save estoura. O catch devolve Failure SEM descartar o rastreamento.
        MotivoDecisaoIsencao colidente = MotivoDecisaoIsencao.Criar(
            codigo, "Colidente", FundamentoIsencao.CadastroUnico, ResultadoPermitido.Indeferido).Value!;
        db.MotivosDecisaoIsencao.Add(colidente);

        string primeiroSave;
        try
        {
            await db.SaveChangesAsync();
            primeiroSave = "sem excecao (a constraint nao barrou)";
        }
        catch (Exception ex)
        {
            primeiroSave = ex.GetType().Name;
        }

        string estadoDoTracker = string.Join(", ", db.ChangeTracker.Entries()
            .Select(e => $"{e.Entity.GetType().Name}={e.State}"));

        // Analogo do SaveChangesAsync que o AutoApplyTransactions dispara
        // depois de o handler retornar Failure (ADR-0119).
        string segundoSave;
        try
        {
            await db.SaveChangesAsync();
            segundoSave = "sem excecao";
        }
        catch (Exception ex)
        {
            segundoSave = ex.GetType().Name;
        }

        db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlAsync($"DELETE FROM selecao.motivos_decisao_isencao WHERE codigo = {codigo}");

        throw new SpikeResultado($"""
            P6 — mecanica do double-save com tracker sujo (ADR-0119)
              1o SaveChanges .......... {primeiroSave}
              tracker apos a falha .... {estadoDoTracker}
              2o SaveChanges .......... {segundoSave}
              (2o estourando = a excecao repete FORA do catch e vira 500)
            """);
    }

    // ---------------------------------------------------------------
    // P7 — 401 emitido por Challenge() DE DENTRO da action (identidade sem
    // jti). O 401 canonico carrega WWW-Authenticate, escrito pelo OnChallenge
    // do esquema. SerializeCachedHeaders persiste so Content-Type, Location e
    // ETag: o replay preserva o desafio?
    // ---------------------------------------------------------------
    [Fact(DisplayName = "P7: 401 de dentro da action — o replay preserva WWW-Authenticate?")]
    public async Task P7_ChallengeReplayado()
    {
        string userId = $"spike-{Guid.NewGuid():N}"[..16];
        string key = Guid.NewGuid().ToString();
        const string Url = "/api/selecao/admin/motivos-decisao-isencao";

        object payload = new
        {
            codigo = $"TEST_MDI_{Guid.NewGuid():N}"[..30].ToUpperInvariant(),
            descricao = "Renda familiar per capita acima do limite legal.",
            fundamento = "CADASTRO_UNICO",
            resultadoPermitido = "INDEFERIDO",
        };

        using HttpClient client = ClienteBase(userId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, PermissaoManterMotivos);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SemJtiHeader, "1");

        using HttpResponseMessage primeira = await PostAsync(client, Url, payload, key);
        IdempotencyEntry? entrada = await LerEntradaAsync(userId, $"POST {Url.ToLowerInvariant()}", key);
        using HttpResponseMessage replay = await PostAsync(client, Url, payload, key);

        throw new SpikeResultado($"""
            P7 — 401 por Challenge() de dentro da action
              1a  status .............. {(int)primeira.StatusCode}
              1a  WWW-Authenticate .... {(primeira.Headers.WwwAuthenticate.Count > 0 ? string.Join(",", primeira.Headers.WwwAuthenticate.Select(h => h.ToString())) : "(ausente)")}
              entrada no store ........ {Descrever(entrada)}
              2a  status .............. {(int)replay.StatusCode}
              2a  WWW-Authenticate .... {(replay.Headers.WwwAuthenticate.Count > 0 ? string.Join(",", replay.Headers.WwwAuthenticate.Select(h => h.ToString())) : "(ausente)")}
              2a  Idempotency-Replayed  {(replay.Headers.TryGetValues("Idempotency-Replayed", out IEnumerable<string>? r) ? string.Join(",", r) : "(ausente)")}
            """);
    }

    // ---------------------------------------------------------------
    // helpers
    // ---------------------------------------------------------------
    private async Task<IdempotencyEntry?> LerEntradaAsync(string userId, string endpoint, string key)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();

        string escopo = $"user:{userId}";
        string endpointCanonico = endpoint;

        return await db.Set<IdempotencyEntry>().AsNoTracking()
            .SingleOrDefaultAsync(e => e.Scope == escopo
                && e.Endpoint == endpointCanonico
                && e.IdempotencyKey == key);
    }

    private async Task<bool> ExisteMotivoAsync(string codigo)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();

        return await db.Database
            .SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM selecao.motivos_decisao_isencao WHERE codigo = {codigo}")
            .SingleAsync() > 0;
    }

    private static string Descrever(IdempotencyEntry? e) => e is null
        ? "AUSENTE (liberada)"
        : $"Status={e.Status}, ResponseStatus={e.ResponseStatus?.ToString(CultureInfo.InvariantCulture) ?? "null"}, ExpiresAt={e.ExpiresAt:O}, CreatedAt={e.CreatedAt:O}";

    private static string Headers(HttpResponseMessage r) =>
        string.Join(" | ", r.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}")
            .Concat(r.Content.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}")));

    private static string Truncar(string s) =>
        s.Length <= 220 ? s.ReplaceLineEndings(" ") : s[..220].ReplaceLineEndings(" ") + "...";

    private HttpClient ClienteComPapeis(string userId, params string[] papeis)
    {
        HttpClient c = ClienteBase(userId);
        c.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', papeis));
        return c;
    }

    private HttpClient ClienteComPermissoes(string userId, params string[] permissoes)
    {
        HttpClient c = ClienteBase(userId);
        c.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, string.Join(',', permissoes));
        return c;
    }

    private HttpClient ClienteBase(string userId)
    {
        HttpClient c = _fixture.Factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        c.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
        c.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, "Spike");
        c.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "spike@unifesspa.edu.br");
        return c;
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, object payload, string key)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(url, UriKind.Relative))
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }
}

/// <summary>Carrega o resultado do spike para o output do runner.</summary>
internal sealed class SpikeResultado(string relatorio) : Exception(relatorio);
