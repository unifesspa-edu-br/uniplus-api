namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// O que os testes do host real fazem para publicar e retificar um processo: semear o processo com o documento do
/// edital confirmado e chamar os endpoints como o operador os chama.
/// </summary>
internal static class PublicacaoPeloEndpointDeTeste
{
    internal static async Task<(Guid ProcessoId, Guid DocumentoId)> SemearProcessoAsync(CascadingApiFactory api, string nome)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, $"{nome} {Guid.CreateVersion7()}");
        return (processo.Id, documento.Id);
    }

    internal static async Task<Guid> SemearDocumentoConfirmadoAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        string hash = string.Concat(Enumerable.Repeat("cd45670189", 7))[..64];
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(processoId, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(2048, hash, TimeProvider.System).IsSuccess.Should().BeTrue();
        await db.DocumentosAPublicar.AddAsync(documento);
        await db.SaveChangesAsync();
        return documento.Id;
    }

    internal static Task<HttpResponseMessage> PublicarAsync(
        HttpClient client, Guid processoId, Guid documentoId, string? tipoAto = null) =>
        PostarComoOperadorAsync(
            client,
            $"/api/selecao/processos-seletivos/{processoId}/publicacao",
            new { numero = "001/2026", documentoEditalId = documentoId, ato = AtoDeTeste(tipoAto ?? DadosDoAtoDeTeste.TipoAbertura) });

    internal static Task<HttpResponseMessage> RetificarAsync(
        HttpClient client, Guid processoId, Guid documentoId, string? tipoAto = null) =>
        PostarComoOperadorAsync(
            client,
            $"/api/selecao/processos-seletivos/{processoId}/retificacoes",
            new
            {
                motivo = "Correção do prazo de inscrição",
                numero = "001/2026-R1",
                documentoEditalId = documentoId,
                ato = AtoDeTeste(tipoAto ?? DadosDoAtoDeTeste.TipoRetificacao),
            });

    private static object AtoDeTeste(string tipoAtoCodigo) => new
    {
        orgao = "CEPS",
        serie = "EDITAL",
        ano = 2026,
        dataDoAto = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        assinante = "Diretor do CEPS",
        tipoAtoCodigo,
    };

    private static async Task<HttpResponseMessage> PostarComoOperadorAsync(HttpClient client, string rota, object corpo)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(rota, UriKind.Relative))
        {
            Content = JsonContent.Create(corpo),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request);
    }

    /// <summary>
    /// O id do ato que criou a versão do certame — decidido pela Seleção, dentro da
    /// transação, e é por ele que o ato é procurado em Publicações depois que a fila drena.
    /// </summary>
    internal static async Task<Guid> ObterAtoIdAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return await db.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId)
            .Select(v => v.AtoCriadorId)
            .SingleAsync();
    }
}
