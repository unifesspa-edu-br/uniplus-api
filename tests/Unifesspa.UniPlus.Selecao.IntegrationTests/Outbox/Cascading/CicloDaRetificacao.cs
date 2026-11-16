namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net.Http.Headers;
using System.Net.Http.Json;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.CicloDaRetificacaoApoio;

/// <summary>
/// O certame publicado sobre o qual o ciclo da retificação é exercitado, e as chamadas HTTP que o ciclo faz
/// (abrir, editar, descartar, fechar, atalho e as duas leituras: a configuração viva e a versão congelada).
/// </summary>
internal sealed record CicloDaRetificacao(CascadingApiFactory Api, HttpClient Client, Guid ProcessoId)
{
    private const string Rota = "/api/selecao/processos-seletivos";

    public Task<HttpResponseMessage> AbrirAsync(string motivo) =>
        EnviarAsync(HttpMethod.Post, $"{Rota}/{ProcessoId}/retificacao-em-curso", new { motivo }, null);

    public Task<HttpResponseMessage> ObterSessaoAsync() =>
        EnviarAsync(HttpMethod.Get, $"{Rota}/{ProcessoId}/retificacao-em-curso", null, null, idempotente: false);

    public Task<HttpResponseMessage> DescartarAsync(string? ifMatch) =>
        EnviarAsync(HttpMethod.Delete, $"{Rota}/{ProcessoId}/retificacao-em-curso", null, ifMatch);

    public Task<HttpResponseMessage> FecharAsync(Guid documentoId, string? ifMatch) =>
        EnviarAsync(
            HttpMethod.Post,
            $"{Rota}/{ProcessoId}/retificacao-em-curso/fechamento",
            CorpoDoAto(documentoId, motivo: null),
            ifMatch);

    public Task<HttpResponseMessage> PutIdentificadorAsync(string? identificador, string? ifMatch) =>
        EnviarAsync(
            HttpMethod.Put,
            $"{Rota}/{ProcessoId}/identificador-legivel",
            new { identificadorLegivel = identificador },
            ifMatch);

    public Task<HttpResponseMessage> AtalhoAsync(Guid documentoId, string motivo) =>
        EnviarAsync(HttpMethod.Post, $"{Rota}/{ProcessoId}/retificacoes", CorpoDoAto(documentoId, motivo), null);

    /// <summary>O peso é o que muda — é uma dimensão REAL da configuração, não um campo decorativo.</summary>
    public Task<HttpResponseMessage> PutEtapasAsync(decimal peso, string? ifMatch) =>
        EnviarAsync(
            HttpMethod.Put,
            $"{Rota}/{ProcessoId}/etapas",
            new[]
            {
                new
                {
                    nome = "Prova Objetiva", carater = 1,
                    tipoEtapaOrigemId = new Guid("019fee1e-7000-7000-8000-000000000001"),
                    peso, notaMinima = (decimal?)null, notaMaxima = 100m, formaDeAvaliacao = "OBJETIVA", ordem = 1, faseCodigo = "RESULTADO_FINAL",
                    produtos = Array.Empty<object>(), bancas = Array.Empty<object>(), recursos = Array.Empty<object>(),
                },
            },
            ifMatch);

    public Task<HttpResponseMessage> ObterRecursoAsync() =>
        EnviarAsync(HttpMethod.Get, $"{Rota}/{ProcessoId}", null, null, idempotente: false,
            accept: "application/vnd.uniplus.processo-seletivo.v1+json");

    public Task<HttpResponseMessage> ObterSnapshotVigenteAsync() =>
        EnviarAsync(HttpMethod.Get, $"{Rota}/{ProcessoId}/snapshot-vigente", null, null, idempotente: false,
            accept: "application/vnd.uniplus.snapshot-vigente-processo-seletivo.v1+json");

    private static object CorpoDoAto(Guid documentoId, string? motivo)
    {
        object ato = new
        {
            orgao = "CEPS",
            serie = "EDITAL",
            ano = 2026,
            dataDoAto = Hoje(),
            assinante = "Diretor do CEPS",
            tipoAtoCodigo = "EDITAL_RETIFICACAO",
        };

        return motivo is null
            ? new
            {
                numero = "001/2026-R",
                documentoEditalId = documentoId,
                ato,
            }
            : new
            {
                motivo,
                numero = "001/2026-R",
                documentoEditalId = documentoId,
                ato,
            };
    }

    private async Task<HttpResponseMessage> EnviarAsync(
        HttpMethod metodo, string rota, object? corpo, string? ifMatch, bool idempotente = true,
        string? accept = null)
    {
        using HttpRequestMessage request = new(metodo, new Uri(rota, UriKind.Relative));
        if (corpo is not null)
        {
            request.Content = JsonContent.Create(corpo);
        }

        // As leituras são versionadas pelo vendor media type (ADR-0028) — sem o Accept
        // certo, o servidor devolve 406 e o teste "falharia" por um motivo que nada tem a
        // ver com o que ele quer provar.
        if (accept is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept", accept);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");

        if (idempotente)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        }

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return await Client.SendAsync(request).ConfigureAwait(false);
    }
}
