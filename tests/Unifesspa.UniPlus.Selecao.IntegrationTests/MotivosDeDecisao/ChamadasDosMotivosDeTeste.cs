namespace Unifesspa.UniPlus.Selecao.IntegrationTests.MotivosDeDecisao;

using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Outbox.Cascading;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>As chamadas HTTP e o cadastro de motivos que os testes da escolha dos motivos nos lugares compartilham.</summary>
internal sealed class ChamadasDosMotivosDeTeste(CascadingFixture fixture, string prefixoDoCodigo)
{
    internal IServiceProvider Services => fixture.Factory.Services;

    internal async Task<MotivoDeDecisao> SemearMotivoAsync(SentidoDoMotivo sentido = SentidoDoMotivo.Desfavoravel)
    {
        MotivoDeDecisao motivo = MotivoDeDecisao.Criar(new CamposDoMotivo(
            TipoMotivoDeDecisaoCodigo.HomologacaoDasInscricoes,
            $"{prefixoDoCodigo}_{Guid.NewGuid():N}"[..40].ToUpperInvariant(),
            "Sem resultado no retorno do INEP para a edição indicada",
            null,
            sentido)).Value!;
        await using AsyncServiceScope escopo = Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        db.MotivosDeDecisao.Add(motivo);
        await db.SaveChangesAsync();

        return motivo;
    }

    /// <summary>Semeia um processo publicável e devolve o documento do edital que a publicação confirma.</summary>
    internal async Task<(Guid ProcessoId, Guid DocumentoId)> SemearProcessoAsync(string nome)
    {
        await using AsyncServiceScope escopo = Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, $"{nome} {Guid.CreateVersion7()}");

        return (processo.Id, documento.Id);
    }

    /// <summary>
    /// Move a janela das fases do processo para o futuro, antes da publicação: a fase só pode sair da retificação se a
    /// avaliação nela ainda não começou.
    /// </summary>
    internal async Task AdiarAsFasesAsync(Guid processoId)
    {
        await using AsyncServiceScope escopo = Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        DateTimeOffset inicio = DateTimeOffset.UtcNow.AddYears(5);
        DateTimeOffset fim = inicio.AddDays(30);
        await db.Database.ExecuteSqlAsync($"UPDATE selecao.fases_cronograma SET inicio = {inicio}, fim = {fim} WHERE processo_seletivo_id = {processoId}");
    }

    internal async Task LimparAsync()
    {
        await using AsyncServiceScope escopo = Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        string padrao = $"{prefixoDoCodigo}\\_%";
        await db.Database.ExecuteSqlAsync($"DELETE FROM selecao.motivos_do_processo WHERE codigo LIKE {padrao} ESCAPE '\\'");
        await db.Database.ExecuteSqlAsync($"DELETE FROM selecao.motivos_de_decisao WHERE codigo LIKE {padrao} ESCAPE '\\'");
    }

    internal async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string url, object? corpo = null, string? etag = null, string? chave = null)
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage request = Requisicao(metodo, url, corpo, etag, chave);

        HttpResponseMessage resposta = await client.SendAsync(request);
        await resposta.Content.LoadIntoBufferAsync();

        return resposta;
    }

    private static HttpRequestMessage Requisicao(HttpMethod metodo, string url, object? corpo, string? etag, string? chave)
    {
        HttpRequestMessage request = new(metodo, new Uri(url, UriKind.Relative));
        if (corpo is not null)
        {
            request.Content = JsonContent.Create(corpo);
        }

        AutenticarComoAdministrador(request, chave);
        if (etag is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", etag);
        }

        return request;
    }

    private static void AutenticarComoAdministrador(HttpRequestMessage request, string? chave)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", chave ?? Guid.CreateVersion7().ToString("N"));
    }

    internal async Task PublicarAsync(Guid processoId, Guid documentoId)
    {
        using HttpResponseMessage publicada = await EnviarAsync(HttpMethod.Post, $"/api/selecao/processos-seletivos/{processoId}/publicacao", CorpoDaPublicacao(documentoId));
        publicada.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent, await publicada.Content.ReadAsStringAsync());
    }

    internal Task<HttpResponseMessage> IncluirAsync(string rota, string? justificativa = null, string? etag = null, string? chave = null) =>
        EnviarAsync(HttpMethod.Put, rota, new { justificativa }, etag, chave);

    internal Task<HttpResponseMessage> RetirarAsync(string rota, string? etag = null) => EnviarAsync(HttpMethod.Delete, rota, etag: etag);

    internal async Task<IReadOnlyList<JsonElement>> LerAsync(Guid processoId, int? versao = null)
    {
        using HttpResponseMessage resposta = await LerRespostaAsync(processoId, versao);
        resposta.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());

        return [.. (await resposta.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()];
    }

    internal async Task<HttpResponseMessage> LerRespostaAsync(Guid processoId, int? versao = null)
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(LugaresDoProcessoDeTeste.RotaDaLeitura(processoId, versao), UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");

        HttpResponseMessage resposta = await client.SendAsync(request);
        await resposta.Content.LoadIntoBufferAsync();

        return resposta;
    }

    internal static async Task<string?> CodigoDoErroAsync(HttpResponseMessage resposta)
    {
        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());

        return doc.RootElement.GetProperty("code").GetString();
    }

    internal static object CorpoDaPublicacao(Guid documentoId, string tipoAtoCodigo = "EDITAL_ABERTURA") => new
    {
        numero = (string?)null,
        documentoEditalId = documentoId,
        ato = new
        {
            orgao = "CEPS",
            serie = "EDITAL",
            ano = 2026,
            dataDoAto = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            assinante = "Diretor do CEPS",
            tipoAtoCodigo,
        },
    };
}
