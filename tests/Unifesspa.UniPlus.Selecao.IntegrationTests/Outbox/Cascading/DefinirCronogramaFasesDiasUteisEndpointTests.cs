namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;

using Kernel.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// CA-08 (issue #1113): <c>PUT .../cronograma-fases</c> fim a fim pelo HTTP — sem dataset
/// de calendário vigente, prazo em DIAS_UTEIS é recusado (422, erro nomeado); com dataset
/// vigente, é aceito (204). Um único <c>[Fact]</c> cobre os dois ramos EM SEQUÊNCIA
/// deliberadamente: a vigência do calendário é estado global (sem filtro por processo,
/// <c>CalendarioVigenteReader.ObterVigenteAsync</c>), e o xUnit não garante ordem entre
/// <c>[Fact]</c>s da mesma classe — dois testes independentes ("sem vigente" / "com
/// vigente") ficariam frágeis à ordem de descoberta entre eles. Um teste sequencial
/// garante essa ordem interna por construção.
/// </summary>
/// <remarks>
/// Isolamento PARCIAL: esta classe é hoje a única, em toda a <see cref="CascadingCollection"/>,
/// que marca um calendário de dias úteis vigente — por isso o cenário "sem vigente" no
/// início do teste vale. Nada aqui impede que uma classe FUTURA da mesma coleção também
/// crie e marque vigente um calendário antes deste teste rodar (a vigência sobrevive entre
/// classes, sem endpoint de "desmarcar"); se isso acontecer, o cenário "sem vigente"
/// passaria a falsear. Se outra classe precisar mexer em calendário, mova este teste para
/// uma coleção própria (Postgres isolado) em vez de reforçar esta suposição.
/// </remarks>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class DefinirCronogramaFasesDiasUteisEndpointTests
{
    // "RESULTADO_PRELIMINAR" é um dos 14 códigos do catálogo fechado de FaseCanonica
    // (FaseCanonicaCatalogo.Codigos) — não é livre. TipoAtoPublicado não exige o mesmo
    // vocabulário, mas reaproveita o código por convenção com o restante da suíte
    // (EnvelopeFechadoE2ETests.CodigoAtoResultadoPreliminar); o guard em
    // SemearTipoDeAtoAsync torna seguro que outra classe da mesma CascadingFixture já
    // tenha semeado o mesmo código.
    private const string CodigoAto = "RESULTADO_PRELIMINAR";

    private readonly CascadingFixture _fixture;

    public DefinirCronogramaFasesDiasUteisEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "CA-08: prazo em DIAS_UTEIS é recusado (422) sem dataset vigente e aceito (204) após marcar um vigente")]
    public async Task DefinirCronogramaComPrazoEmDiasUteis_RecusaSemVigenteEAceitaComVigente()
    {
        CascadingApiFactory api = _fixture.Factory;
        HttpClient client = api.CreateClient();
        string sufixo = Guid.CreateVersion7().ToString("N")[..8];

        (Guid processoId, Guid faseCanonicaId) = await SemearProcessoEFaseCanonicaAsync(api, sufixo);
        await SemearTipoDeAtoAsync(api);

        object[] fases = [CorpoFaseComRegraRecurso(faseCanonicaId)];

        HttpResponseMessage semVigente = await PutCronogramaFasesAsync(client, processoId, fases);

        semVigente.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using (JsonDocument corpoSemVigente = JsonDocument.Parse(await semVigente.Content.ReadAsStringAsync()))
        {
            JsonElement problema = corpoSemVigente.RootElement;
            problema.GetProperty("code").GetString().Should().Be(
                "uniplus.selecao.regra_recurso_fase.prazo_em_dias_uteis_sem_calendario");
            problema.GetProperty("title").GetString().Should().Contain("vigente");
            problema.GetProperty("detail").GetString().Should().Contain("vigente");
        }

        Guid calendarioId = await CriarCalendarioAsync(client, sufixo);
        HttpResponseMessage marcarVigente = await MarcarVigenteAsync(client, calendarioId);
        marcarVigente.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage comVigente = await PutCronogramaFasesAsync(client, processoId, fases);

        comVigente.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "com dataset vigente, DIAS_UTEIS deixa de ser recusado — o gatilho da recusa mudou (issue #1113), não o comportamento fail-closed em si");
    }

    private static object CorpoFaseComRegraRecurso(Guid faseCanonicaId) => new
    {
        ordem = 1,
        faseCanonicaId,
        inicio = new DateTimeOffset(2026, 3, 25, 0, 0, 0, TimeSpan.Zero),
        fim = new DateTimeOffset(2026, 3, 25, 18, 0, 0, TimeSpan.Zero),
        atoProduzidoCodigo = CodigoAto,
        tiposBancaIds = Array.Empty<Guid>(),
        regraRecurso = new
        {
            regraCodigo = RegraPrazoRecursoCodigo.AncoradoEmAto,
            regraVersao = "v1",
            prazoValor = 3,
            prazoUnidade = "diasUteis",
            atoAncoraCodigo = CodigoAto,
            suspensividadePrimeiraInstanciaValor = (decimal?)null,
            suspensividadePrimeiraInstanciaUnidade = (string?)null,
            suspensividadeSegundaInstanciaValor = (decimal?)null,
            suspensividadeSegundaInstanciaUnidade = (string?)null,
        },
    };

    private static async Task<(Guid ProcessoId, Guid FaseCanonicaId)> SemearProcessoEFaseCanonicaAsync(CascadingApiFactory api, string sufixo)
    {
        Guid processoId;
        await using (AsyncServiceScope scopeSelecao = api.Services.CreateAsyncScope())
        {
            SelecaoDbContext db = scopeSelecao.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(
                db, $"PS Dias Úteis {sufixo}", cidade: ("1504208", "Marabá", "PA"));
            processoId = processo.Id;
        }

        Guid faseCanonicaId;
        await using (AsyncServiceScope scopeConfig = api.Services.CreateAsyncScope())
        {
            ConfiguracaoDbContext config = scopeConfig.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
            Result<FaseCanonica> faseResult = FaseCanonica.Criar(
                CodigoAto, "Resultado preliminar (issue #1113)", null, "CEPS",
                agrupaEtapas: false, permiteComplementacao: false, baseLegal: null,
                produzResultado: true, resultadoDefinitivo: false, coletaInscricao: false,
                origemData: "PROPRIA");
            faseResult.IsSuccess.Should().BeTrue(faseResult.Error?.Message);
            config.FasesCanonicas.Add(faseResult.Value!);
            await config.SaveChangesAsync();
            faseCanonicaId = faseResult.Value!.Id;
        }

        return (processoId, faseCanonicaId);
    }

    private static async Task SemearTipoDeAtoAsync(CascadingApiFactory api)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        PublicacoesDbContext db = scope.ServiceProvider.GetRequiredService<PublicacoesDbContext>();
        if (await db.Set<TipoAtoPublicado>().AnyAsync(t => t.Codigo == CodigoAto))
        {
            return;
        }

        Result<TipoAtoPublicado> tipoResult = TipoAtoPublicado.Criar(
            CodigoAto, "Resultado preliminar (issue #1113)",
            congelaConfiguracao: false, unicoPorObjeto: false, efeitoIrreversivel: true,
            new DateOnly(2020, 1, 1), vigenciaFim: null, baseLegal: null);
        tipoResult.IsSuccess.Should().BeTrue(tipoResult.Error?.Message);

        await db.Set<TipoAtoPublicado>().AddAsync(tipoResult.Value!);
        await db.SaveChangesAsync();
    }

    private static async Task<HttpResponseMessage> PutCronogramaFasesAsync(HttpClient client, Guid processoId, object[] fases)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Put,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/cronograma-fases", UriKind.Relative))
        {
            Content = JsonContent.Create(fases),
        };
        Autenticar(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
        return await client.SendAsync(request).ConfigureAwait(false);
    }

    private static async Task<Guid> CriarCalendarioAsync(HttpClient client, string sufixo)
    {
        object corpo = new
        {
            versaoDataset = $"1113-{sufixo}",
            diasNaoUteis = new[]
            {
                new { abrangencia = "NACIONAL", municipioIbge = (string?)null, uf = (string?)null, data = "2027-01-01", descricao = "Confraternização Universal" },
            },
        };
        using HttpRequestMessage request = new(
            HttpMethod.Post, new Uri("/api/configuracao/admin/calendarios-dias-uteis", UriKind.Relative))
        {
            Content = JsonContent.Create(corpo),
        };
        Autenticar(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());

        HttpResponseMessage resposta = await client.SendAsync(request).ConfigureAwait(false);
        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
        return await resposta.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<HttpResponseMessage> MarcarVigenteAsync(HttpClient client, Guid calendarioId)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Post,
            new Uri($"/api/configuracao/admin/calendarios-dias-uteis/{calendarioId}/vigente", UriKind.Relative));
        Autenticar(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
        return await client.SendAsync(request).ConfigureAwait(false);
    }

    private static void Autenticar(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
    }

    private static string MakeIdempotencyKey() => Guid.CreateVersion7().ToString("N");
}
