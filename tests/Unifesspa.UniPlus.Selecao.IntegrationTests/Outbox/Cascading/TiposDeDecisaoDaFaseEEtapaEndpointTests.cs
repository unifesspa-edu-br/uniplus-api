namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// Os tipos de decisão declarados pela fase e pela etapa chegando ao cliente pelo HTTP: o tipo repetido e o
/// tipo fora do vocabulário saem como 422 com a posição de cada um em <c>errors[]</c>, junto das demais recusas
/// da mesma fase ou etapa e na ordem da declaração, e a recusa de forma do validator do comando sai sozinha,
/// antes de qualquer tipo ser conferido.
/// </summary>
/// <remarks>
/// A fase e a etapa são julgadas contra o cadastro, por isso os corpos usam a fase canônica e o tipo de etapa
/// semeados.
/// </remarks>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class TiposDeDecisaoDaFaseEEtapaEndpointTests
{
    private const string Classificacao = "CLASSIFICACAO";
    private const string Isencao = "ISENCAO_DA_TAXA";
    private const string TipoForaDoVocabulario = "RECURSO_DA_BANCA";
    private const string CodigoDeTipoInvalido = "uniplus.selecao.motivo_de_decisao.tipo_invalido";
    private const string CodigoDeTipoDuplicado = "uniplus.selecao.motivo_de_decisao.tipo_de_decisao_duplicado";
    private const string PrefixoDaValidacao = "uniplus.validacao";
    private const string CodigoDeJanelaInvertidaDaFase = "uniplus.selecao.fase_cronograma.janela_invertida";
    private const string CodigoDeJanelaInvertidaDaEtapa = "uniplus.selecao.etapa_processo.janela_invertida";
    private const string FasePreliminar = "HETEROIDENTIFICACAO";
    private const string FaseDefinitiva = "HOMOLOGACAO_RESULTADO_FINAL";
    private static readonly Guid TipoDeEtapaSemeado = new("019fee1e-7000-7000-8000-000000000001");
    private static readonly DateTimeOffset Inicio = new(2027, 2, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FimAntesDoInicio = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FimDepoisDoInicio = new(2027, 3, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly CascadingFixture _fixture;

    public TiposDeDecisaoDaFaseEEtapaEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Fases com tipo repetido e tipo fora do vocabulário devolvem 422 com a posição de cada tipo")]
    public async Task Fases_TipoRepetidoETipoInvalido_422PorPosicao()
    {
        object[] fases =
        [
            Fase(ordem: 1, await FaseCanonicaAsync(FasePreliminar), Inicio, FimDepoisDoInicio, [Classificacao, Isencao, Classificacao]),
            Fase(ordem: 2, await FaseCanonicaAsync(FaseDefinitiva), Inicio, FimDepoisDoInicio, [TipoForaDoVocabulario]),
        ];

        Recusa recusa = await EnviarAsync("cronograma-fases", fases);

        recusa.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        recusa.Erros.Should().BeEquivalentTo(
        [
            ("fases[0].tiposDeDecisao[2]", CodigoDeTipoDuplicado),
            ("fases[1].tiposDeDecisao[0]", CodigoDeTipoInvalido),
        ]);
    }

    [Fact(DisplayName = "Etapas com tipo repetido e tipo fora do vocabulário devolvem 422 com a posição de cada tipo")]
    public async Task Etapas_TipoRepetidoETipoInvalido_422PorPosicao()
    {
        object[] etapas =
        [
            Etapa(ordem: 1, TipoDeEtapaSemeado, [Classificacao, Classificacao]),
            Etapa(ordem: 2, TipoDeEtapaSemeado, [TipoForaDoVocabulario]),
        ];

        Recusa recusa = await EnviarAsync("etapas", etapas);

        recusa.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        recusa.Erros.Should().BeEquivalentTo(
        [
            ("etapas[0].tiposDeDecisao[1]", CodigoDeTipoDuplicado),
            ("etapas[1].tiposDeDecisao[0]", CodigoDeTipoInvalido),
        ]);
    }

    [Fact(DisplayName = "Fase com tipo inválido e janela invertida devolve a janela e o tipo em errors[], nessa ordem, e a raiz traz a janela")]
    public async Task Fase_TipoInvalidoEJanelaInvertida_422NaOrdemDaDeclaracao()
    {
        object[] fases = [Fase(ordem: 1, await FaseCanonicaAsync(FasePreliminar), Inicio, FimAntesDoInicio, [TipoForaDoVocabulario])];

        Recusa recusa = await EnviarAsync("cronograma-fases", fases);

        recusa.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        recusa.CodigoDaRaiz.Should().Be(CodigoDeJanelaInvertidaDaFase);
        recusa.Erros.Should().Equal(
            ("fases[0].fim", CodigoDeJanelaInvertidaDaFase),
            ("fases[0].tiposDeDecisao[0]", CodigoDeTipoInvalido));
    }

    [Fact(DisplayName = "Etapa com tipo inválido e janela invertida devolve a janela e o tipo em errors[], nessa ordem, e a raiz traz a janela")]
    public async Task Etapa_TipoInvalidoEJanelaInvertida_422NaOrdemDaDeclaracao()
    {
        object[] etapas = [Etapa(ordem: 1, TipoDeEtapaSemeado, [TipoForaDoVocabulario], Inicio, FimAntesDoInicio)];

        Recusa recusa = await EnviarAsync("etapas", etapas);

        recusa.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        recusa.CodigoDaRaiz.Should().Be(CodigoDeJanelaInvertidaDaEtapa);
        recusa.Erros.Should().Equal(
            ("etapas[0].fim", CodigoDeJanelaInvertidaDaEtapa),
            ("etapas[0].tiposDeDecisao[0]", CodigoDeTipoInvalido));
    }

    [Fact(DisplayName = "Fase com a ordem fora da forma e tipos inválidos devolve só a recusa de forma do validator")]
    public async Task Fases_FormaInvalidaETiposInvalidos_SoAFormaDoValidator()
    {
        object[] fases =
        [
            Fase(ordem: 0, [TipoForaDoVocabulario]),
            Fase(ordem: 2, [Classificacao, Classificacao]),
        ];

        Recusa recusa = await EnviarAsync("cronograma-fases", fases);

        recusa.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        recusa.CodigoDaRaiz.Should().Be(PrefixoDaValidacao);
        recusa.Erros.Select(static e => e.Campo).Should().BeEquivalentTo(["fases[0].ordem"]);
        recusa.Erros.Should().AllSatisfy(static e => e.Codigo.Should().StartWith(PrefixoDaValidacao + "."));
    }

    [Fact(DisplayName = "Etapa sem o tipo de origem e tipos inválidos devolve só a recusa de forma do validator")]
    public async Task Etapas_FormaInvalidaETiposInvalidos_SoAFormaDoValidator()
    {
        object[] etapas =
        [
            Etapa(ordem: 1, Guid.Empty, [TipoForaDoVocabulario]),
            Etapa(ordem: 2, Guid.CreateVersion7(), [Classificacao, Classificacao]),
        ];

        Recusa recusa = await EnviarAsync("etapas", etapas);

        recusa.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        recusa.CodigoDaRaiz.Should().Be(PrefixoDaValidacao);
        recusa.Erros.Select(static e => e.Campo).Should().BeEquivalentTo(["etapas[0].tipoEtapaOrigemId"]);
        recusa.Erros.Should().AllSatisfy(static e => e.Codigo.Should().StartWith(PrefixoDaValidacao + "."));
    }

    private static object Fase(
        int ordem, Guid faseCanonicaId, DateTimeOffset? inicio, DateTimeOffset? fim, string[] tiposDeDecisao) => new
        {
            ordem,
            faseCanonicaId,
            inicio,
            fim,
            produtos = Array.Empty<object>(),
            emiteParecerIndividual = false,
            bancasRequeridas = Array.Empty<object>(),
            regraRecurso = (object?)null,
            tiposDeDecisao,
        };

    private static object Fase(int ordem, string[] tiposDeDecisao) =>
        Fase(ordem, Guid.CreateVersion7(), inicio: null, fim: null, tiposDeDecisao);

    private static object Etapa(int ordem, Guid tipoEtapaOrigemId, string[] tiposDeDecisao) =>
        Etapa(ordem, tipoEtapaOrigemId, tiposDeDecisao, inicio: null, fim: null);

    private static object Etapa(
        int ordem, Guid tipoEtapaOrigemId, string[] tiposDeDecisao, DateTimeOffset? inicio, DateTimeOffset? fim) => new
        {
            nome = $"Etapa {ordem}",
            carater = CaraterEtapa.Classificatoria.Codigo(),
            tipoEtapaOrigemId,
            peso = 1.0m,
            notaMinima = (decimal?)null,
            ordem,
            inicio,
            fim,
            produtos = Array.Empty<object>(),
            bancas = Array.Empty<object>(),
            recursos = Array.Empty<object>(),
            tiposDeDecisao,
        };

    private async Task<Recusa> EnviarAsync(string recurso, object corpo)
    {
        Guid processoId = await SemearProcessoAsync();
        using HttpRequestMessage request = new(
            HttpMethod.Put,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/{recurso}", UriKind.Relative))
        {
            Content = JsonContent.Create(corpo),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));

        using HttpResponseMessage resposta = await _fixture.Factory.CreateClient().SendAsync(request);
        return await Recusa.LerAsync(resposta);
    }

    private async Task<Guid> FaseCanonicaAsync(string codigo)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        List<FaseCanonica> vivas = await db.FasesCanonicas.AsNoTracking().ToListAsync();
        return vivas.Single(fase => string.Equals(fase.Codigo.Valor, codigo, StringComparison.Ordinal)).Id;
    }

    private async Task<Guid> SemearProcessoAsync()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return (await ProcessoSeletivoDeTeste.GravarAsync(db, $"PS tipos de decisão {Guid.CreateVersion7()}")).Id;
    }

    private sealed record Recusa(HttpStatusCode Status, string? CodigoDaRaiz, (string? Campo, string? Codigo)[] Erros)
    {
        public static async Task<Recusa> LerAsync(HttpResponseMessage resposta)
        {
            using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
            JsonElement raiz = problema.RootElement;
            (string?, string?)[] erros = raiz.TryGetProperty("errors", out JsonElement lista)
                ? [.. lista.EnumerateArray().Select(static e => (e.GetProperty("field").GetString(), e.GetProperty("code").GetString()))]
                : [];
            return new Recusa(resposta.StatusCode, raiz.GetProperty("code").GetString(), erros);
        }
    }
}
