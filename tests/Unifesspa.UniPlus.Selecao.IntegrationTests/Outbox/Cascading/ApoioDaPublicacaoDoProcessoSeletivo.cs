namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;
using Domain.Events;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

// Cenário fim-a-fim do fluxo de referência ADR-0005:
// HTTP request → PublicarProcessoSeletivoCommand → handler convention-based
// produtivo → ProcessoSeletivo.Publicar(, ExemploDeProcessoSeletivoDadosDoEdital.ContextoRico()) emite ProcessoPublicadoEvent via
// AddDomainEvent → handler retorna (Result, IEnumerable<object>) com o
// evento drenado por DequeueDomainEvents().Cast<object>() →
// CaptureCascadingMessages persiste envelope na MESMA transação do
// SaveChanges → listener da queue PG entrega ao subscritor
// (ProcessoPublicadoSubscriberHandler do teste, que registra no coletor; o
// ProcessoPublicadoEventHandler produtivo também é invocado pela fan-out,
// executa logging estruturado e não interfere no estado do coletor).

internal sealed class ApoioDaPublicacaoDoProcessoSeletivo
{
    private readonly CascadingFixture _fixture;

    internal ApoioDaPublicacaoDoProcessoSeletivo(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    internal static async Task<HttpResponseMessage> GetConformidadeAsync(HttpClient client, Guid processoId)
    {
        using HttpRequestMessage request = new(HttpMethod.Get,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/conformidade", UriKind.Relative));
        AppendTestAuth(request);
        return await client.SendAsync(request).ConfigureAwait(false);
    }

    /// <summary>
    /// Semeia via <paramref name="semear"/>, publica e confirma que o 422 tem o <paramref
    /// name="codigoEsperado"/> e que <c>pendencias</c> contém o item de código <paramref
    /// name="codigoDoItemVermelho"/> — corpo comum aos quatro grupos de gate estrutural
    /// (CA-01 a CA-04).
    /// </summary>
    /// <remarks>
    /// A asserção é pelo código do item, não pela frase: a mensagem é redação e muda sem que
    /// a invariante mude, e um teste preso ao texto quebraria na primeira revisão editorial
    /// sem nada ter regredido.
    /// </remarks>
    internal async Task AssertGateEstruturalNaoConformeAsync(
        Func<UniPlusDbContext, string, Task<(ProcessoSeletivo Processo, DocumentoAPublicar Documento)>> semear, string codigoEsperado, string codigoDoItemVermelho, string nomeDoCenario)
    {
        ArgumentNullException.ThrowIfNull(semear);

        using HttpResponseMessage response = await SemearEPublicarAsync(semear, nomeDoCenario);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be(codigoEsperado);
        doc.RootElement.GetProperty("pendencias").EnumerateArray()
            .Select(e => e.GetProperty("codigo").GetString())
            .Should().Contain(codigoDoItemVermelho);
    }

    private async Task<HttpResponseMessage> SemearEPublicarAsync(
        Func<UniPlusDbContext, string, Task<(ProcessoSeletivo Processo, DocumentoAPublicar Documento)>> semear, string nomeDoCenario)
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient client = api.CreateClient();
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await semear(db, $"{nomeDoCenario} {Guid.CreateVersion7()}");

        return await PostPublicarAsync(client, processo.Id, documento.Id, MakeIdempotencyKey());
    }

    internal static object NovoCorpoPublicacao(Guid documentoEditalId, string? numero = null) => new
    {
        numero,
        documentoEditalId,
        ato = new
        {
            orgao = "CEPS",
            serie = "EDITAL",
            ano = 2026,
            dataDoAto = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            assinante = "Diretor do CEPS",
            tipoAtoCodigo = "EDITAL_ABERTURA",
        },
    };

    internal static async Task<HttpResponseMessage> PostPublicarAsync(
        HttpClient client, Guid processoId, Guid documentoEditalId, string idempotencyKey)
    {
        using HttpRequestMessage request = new(HttpMethod.Post,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/publicacao", UriKind.Relative))
        {
            Content = JsonContent.Create(NovoCorpoPublicacao(documentoEditalId)),
        };
        AppendTestAuth(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request).ConfigureAwait(false);
    }

    internal static string MakeIdempotencyKey() => Guid.CreateVersion7().ToString("N");

    /// <summary>
    /// Adiciona Authorization Bearer + role plataforma-admin ao request —
    /// exigida por [Authorize(Roles = "plataforma-admin")] no controller.
    /// </summary>
    internal static void AppendTestAuth(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
    }

    internal static async Task<(Guid ProcessoId, Guid DocumentoId)> SemearAsync(CascadingApiFactory api, string nome)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel { Nome = $"{nome} {Guid.CreateVersion7()}" });

        return (processo.Id, documento.Id);
    }
}
