namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// Identificador legível pelo HTTP (issue #1479): as recusas precisam aflorar com o código
/// nomeado do catálogo, e a imutabilidade tem de valer sobre um certame publicado de verdade.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class IdentificadorLegivelEndpointTests
{
    private const string Rota = "/api/selecao/processos-seletivos";

    private readonly CascadingFixture _fixture;

    public IdentificadorLegivelEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Em rascunho, o identificador declarado volta na leitura administrativa")]
    public async Task Rascunho_DefineERelê()
    {
        (HttpClient client, Guid processoId, _) = await SemearAsync(nameof(Rascunho_DefineERelê));
        string novo = IdentificadoresDeTeste.NovoValor();

        HttpResponseMessage resposta = await PutIdentificadorAsync(client, processoId, novo, ifMatch: null);

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using HttpRequestMessage obter = new(HttpMethod.Get, new Uri($"{Rota}/{processoId}", UriKind.Relative));
        AppendTestAuth(obter);
        HttpResponseMessage leitura = await client.SendAsync(obter);
        using JsonDocument corpo = JsonDocument.Parse(await leitura.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("identificadorLegivel").GetString().Should().Be(novo);
    }

    [Fact(DisplayName = "Identificador em uso por outro processo é recusado com 409 e código nomeado")]
    public async Task EmUso_Conflito()
    {
        (HttpClient client, Guid processoId, _) = await SemearAsync(nameof(EmUso_Conflito));
        (_, Guid outroId, _) = await SemearAsync($"{nameof(EmUso_Conflito)} outro");
        string doOutro = await LerIdentificadorAsync(outroId);

        HttpResponseMessage resposta = await PutIdentificadorAsync(client, processoId, doOutro, ifMatch: null);

        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await LerCodigoAsync(resposta)).Should().Be("uniplus.selecao.processo_seletivo.identificador_legivel_em_uso");
    }

    [Theory(DisplayName = "Formato inválido é recusado com 422 e o código da causa")]
    [InlineData("PSIQ-2026", "uniplus.selecao.processo_seletivo.identificador_legivel_formato_invalido")]
    [InlineData("ps", "uniplus.selecao.processo_seletivo.identificador_legivel_tamanho")]
    [InlineData("a1b2c3d4-5e6f-4a7b-8c9d-0e1f2a3b4c5d", "uniplus.selecao.processo_seletivo.identificador_legivel_com_formato_de_guid")]
    public async Task FormatoInvalido_Recusa(string valor, string codigo)
    {
        (HttpClient client, Guid processoId, _) = await SemearAsync(nameof(FormatoInvalido_Recusa));

        HttpResponseMessage resposta = await PutIdentificadorAsync(client, processoId, valor, ifMatch: null);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await LerCodigoAsync(resposta)).Should().Be(codigo);
    }

    [Fact(DisplayName = "Publicar sem identificador legível é recusado com 422 e código nomeado")]
    public async Task Publicar_SemIdentificador_Recusa()
    {
        (HttpClient client, Guid processoId, Guid documentoId) = await SemearAsync(nameof(Publicar_SemIdentificador_Recusa));
        (await PutIdentificadorAsync(client, processoId, null, ifMatch: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage resposta = await PublicarAsync(client, processoId, documentoId);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await LerCodigoAsync(resposta)).Should().Be("uniplus.selecao.processo_seletivo.identificador_legivel_ausente");
    }

    [Fact(DisplayName = "Identificador de certame publicado não muda, nem na sessão de retificação")]
    public async Task Publicado_Imutavel()
    {
        (HttpClient client, Guid processoId, Guid documentoId) = await SemearAsync(nameof(Publicado_Imutavel));
        string original = await LerIdentificadorAsync(processoId);
        (await PublicarAsync(client, processoId, documentoId)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        HttpResponseMessage abertura = await AbrirRetificacaoAsync(client, processoId);
        abertura.StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage resposta = await PutIdentificadorAsync(
            client, processoId, IdentificadoresDeTeste.NovoValor(), abertura.Headers.ETag!.ToString());

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await LerCodigoAsync(resposta)).Should().Be("uniplus.selecao.processo_seletivo.identificador_legivel_imutavel");
        (await LerIdentificadorAsync(processoId)).Should().Be(original);
    }

    private async Task<(HttpClient Client, Guid ProcessoId, Guid DocumentoId)> SemearAsync(string nome)
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);

        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, DocumentoEdital documento) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, $"{nome} {Guid.CreateVersion7()}");
        return (api.CreateClient(), processo.Id, documento.Id);
    }

    private async Task<string> LerIdentificadorAsync(Guid processoId)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        ProcessoSeletivo processo = await db.ProcessosSeletivos.AsNoTracking().SingleAsync(p => p.Id == processoId);
        return processo.IdentificadorLegivel!.Value.Valor;
    }

    private static async Task<HttpResponseMessage> PutIdentificadorAsync(
        HttpClient client, Guid processoId, string? identificador, string? ifMatch)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Put, new Uri($"{Rota}/{processoId}/identificador-legivel", UriKind.Relative))
        {
            Content = JsonContent.Create(new { identificadorLegivel = identificador }),
        };
        AppendTestAuth(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> PublicarAsync(HttpClient client, Guid processoId, Guid documentoId)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri($"{Rota}/{processoId}/publicacao", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                numero = "001/2026",
                documentoEditalId = documentoId,
                ato = new
                {
                    orgao = "CEPS",
                    serie = "EDITAL",
                    ano = 2026,
                    dataPublicacao = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    assinante = "Diretor do CEPS",
                    tipoAtoCodigo = "EDITAL_ABERTURA",
                },
            }),
        };
        AppendTestAuth(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> AbrirRetificacaoAsync(HttpClient client, Guid processoId)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Post, new Uri($"{Rota}/{processoId}/retificacao-em-curso", UriKind.Relative))
        {
            Content = JsonContent.Create(new { motivo = "Correção do prazo" }),
        };
        AppendTestAuth(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
        return await client.SendAsync(request);
    }

    private static async Task<string?> LerCodigoAsync(HttpResponseMessage resposta)
    {
        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return corpo.RootElement.GetProperty("code").GetString();
    }

    private static string MakeIdempotencyKey() => Guid.CreateVersion7().ToString("N");

    private static void AppendTestAuth(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
    }
}
