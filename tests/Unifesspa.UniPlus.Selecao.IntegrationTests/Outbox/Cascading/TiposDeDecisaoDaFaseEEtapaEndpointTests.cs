namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Enums;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// Os tipos de decisão declarados pela fase e pela etapa chegando ao cliente pelo HTTP: o tipo repetido e o
/// tipo fora do vocabulário saem como 422 com a posição de cada um em <c>errors[]</c>, e a recusa de forma do
/// validator do comando sai sozinha, antes de qualquer tipo ser conferido.
/// </summary>
/// <remarks>
/// Os tipos são conferidos no estágio do payload, antes de qualquer cadastro; por isso os corpos usam
/// identificadores de catálogo que não precisam existir.
/// </remarks>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class TiposDeDecisaoDaFaseEEtapaEndpointTests
{
    private const string Classificacao = TipoMotivoDeDecisaoCodigo.Classificacao;
    private const string Isencao = TipoMotivoDeDecisaoCodigo.IsencaoDaTaxa;
    private const string TipoForaDoVocabulario = "RECURSO_DA_BANCA";
    private const string CodigoDeTipoInvalido = "uniplus.selecao.motivo_de_decisao.tipo_invalido";
    private const string CodigoDeTipoDuplicado = "uniplus.selecao.motivo_de_decisao.tipo_de_decisao_duplicado";
    private const string PrefixoDaValidacao = "uniplus.validacao";

    private readonly CascadingFixture _fixture;

    public TiposDeDecisaoDaFaseEEtapaEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Fases com tipo repetido e tipo fora do vocabulário devolvem 422 com a posição de cada tipo")]
    public async Task Fases_TipoRepetidoETipoInvalido_422PorPosicao()
    {
        object[] fases =
        [
            Fase(ordem: 1, [Classificacao, Isencao, Classificacao]),
            Fase(ordem: 2, [TipoForaDoVocabulario]),
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
            Etapa(ordem: 1, Guid.CreateVersion7(), [Classificacao, Classificacao]),
            Etapa(ordem: 2, Guid.CreateVersion7(), [TipoForaDoVocabulario]),
        ];

        Recusa recusa = await EnviarAsync("etapas", etapas);

        recusa.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        recusa.Erros.Should().BeEquivalentTo(
        [
            ("etapas[0].tiposDeDecisao[1]", CodigoDeTipoDuplicado),
            ("etapas[1].tiposDeDecisao[0]", CodigoDeTipoInvalido),
        ]);
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

    private static object Fase(int ordem, string[] tiposDeDecisao) => new
    {
        ordem,
        faseCanonicaId = Guid.CreateVersion7(),
        inicio = (DateTimeOffset?)null,
        fim = (DateTimeOffset?)null,
        produtos = Array.Empty<object>(),
        faseConcluinteCodigo = (string?)null,
        emiteParecerIndividual = false,
        bancasRequeridas = Array.Empty<object>(),
        regraRecurso = (object?)null,
        tiposDeDecisao,
    };

    private static object Etapa(int ordem, Guid tipoEtapaOrigemId, string[] tiposDeDecisao) => new
    {
        nome = $"Etapa {ordem}",
        carater = (int)CaraterEtapa.Classificatoria,
        tipoEtapaOrigemId,
        peso = 1.0m,
        notaMinima = (decimal?)null,
        ordem,
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
