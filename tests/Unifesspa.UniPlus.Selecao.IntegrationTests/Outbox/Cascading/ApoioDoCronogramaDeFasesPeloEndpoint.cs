namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Os passos que os testes do cronograma de fases pelo HTTP repetem: ler uma fase do catálogo
/// canônico, semear o processo em rascunho, enviar o <c>PUT</c> do cronograma, ler o processo e
/// ler o <c>errors[]</c> da recusa.
/// </summary>
internal static class ApoioDoCronogramaDeFasesPeloEndpoint
{
    internal sealed record ErroDoProblema(string? Campo, string? Codigo);

    internal static async Task<HttpResponseMessage> EnviarCronogramaAsync(
        HttpClient client, Guid processoId, object[] fases)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, RotaDoCronograma(processoId))
        {
            Content = JsonContent.Create(fases),
        };
        return await EnviarComChaveDeIdempotenciaAsync(client, request).ConfigureAwait(false);
    }

    /// <summary>
    /// Envia o corpo como texto, sem passar pelo serializador do teste: é o texto RFC 3339 que
    /// trafega que está sob prova, e o serializador decidiria o offset no lugar do cliente.
    /// </summary>
    internal static async Task<HttpResponseMessage> EnviarCronogramaComCorpoCruAsync(
        HttpClient client, Guid processoId, string corpoJson)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, RotaDoCronograma(processoId))
        {
            Content = new StringContent(corpoJson, Encoding.UTF8, "application/json"),
        };
        return await EnviarComChaveDeIdempotenciaAsync(client, request).ConfigureAwait(false);
    }

    internal static async Task<ProcessoSeletivoDto> LerProcessoPelaApiAsync(HttpClient client, Guid processoId)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            new Uri($"/api/selecao/processos-seletivos/{processoId}", UriKind.Relative));
        Autenticar(request);
        using HttpResponseMessage resposta = await client.SendAsync(request).ConfigureAwait(false);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        ProcessoSeletivoDto? processo = await resposta.Content
            .ReadFromJsonAsync<ProcessoSeletivoDto>(JsonDoContrato.Opcoes).ConfigureAwait(false);
        return processo!;
    }

    internal static async Task<ErroDoProblema[]> LerErrosDoProblemaAsync(
        HttpResponseMessage resposta, string motivoDoArray = "")
    {
        using JsonDocument problema = JsonDocument.Parse(
            await resposta.Content.ReadAsStringAsync().ConfigureAwait(false));

        problema.RootElement.TryGetProperty("errors", out JsonElement erros).Should().BeTrue(motivoDoArray);

        return [.. erros.EnumerateArray()
            .Select(e => new ErroDoProblema(
                Campo: e.GetProperty("field").GetString(),
                Codigo: e.GetProperty("code").GetString()))];
    }

    internal static async Task<FaseCanonica?> ProcurarFaseCanonicaAsync(CascadingApiFactory api, string codigo)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext config = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        // O catálogo de fases vivas é pequeno — materializar é mais barato que traduzir a
        // comparação do value object Codigo para SQL.
        List<FaseCanonica> vivas = await config.FasesCanonicas.AsNoTracking().ToListAsync().ConfigureAwait(false);
        return vivas.Find(f => string.Equals(f.Codigo.Valor, codigo, StringComparison.Ordinal));
    }

    internal static async Task<Guid> ObterFaseCanonicaSemeadaAsync(CascadingApiFactory api, string codigo)
    {
        FaseCanonica? fase = await ProcurarFaseCanonicaAsync(api, codigo).ConfigureAwait(false);

        fase.Should().NotBeNull($"'{codigo}' faz parte do catálogo canônico semeado");
        return fase!.Id;
    }

    internal static async Task<Guid> SemearProcessoEmRascunhoAsync(
        CascadingApiFactory api, string nome, OrigemCandidatos origem)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        ProcessoSeletivo processo = NovoProcessoEmRascunho($"{nome} {Guid.CreateVersion7()}", origem);

        await db.ProcessosSeletivos.AddAsync(processo).ConfigureAwait(false);
        await db.SaveChangesAsync().ConfigureAwait(false);
        return processo.Id;
    }

    internal static void Autenticar(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
    }

    private static ProcessoSeletivo NovoProcessoEmRascunho(string nome, OrigemCandidatos origem) =>
        ProcessoSeletivo.Criar(
            nome,
            TiposDeProcessoDeTeste.SiSU,
            origem,
            Guid.CreateVersion7(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!,
            IdentificadoresDeTeste.Novo());

    private static Uri RotaDoCronograma(Guid processoId) =>
        new($"/api/selecao/processos-seletivos/{processoId}/cronograma-fases", UriKind.Relative);

    private static async Task<HttpResponseMessage> EnviarComChaveDeIdempotenciaAsync(
        HttpClient client, HttpRequestMessage request)
    {
        Autenticar(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request).ConfigureAwait(false);
    }
}
