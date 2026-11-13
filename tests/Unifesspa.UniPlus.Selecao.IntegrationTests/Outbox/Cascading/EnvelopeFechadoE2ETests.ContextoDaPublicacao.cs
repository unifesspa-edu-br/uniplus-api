namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed partial class EnvelopeFechadoE2ETests
{
    private sealed partial record Contexto
    {
        public async Task<ProcessoSeletivoDto> ObterProcessoAsync()
        {
            HttpResponseMessage resposta = await EnviarAsync(
                HttpMethod.Get, $"{Rota}/{ProcessoId}", null, ifMatch: null, idempotente: false,
                accept: "application/vnd.uniplus.processo-seletivo.v1+json").ConfigureAwait(false);
            resposta.StatusCode.Should().Be(HttpStatusCode.OK, "GET do recurso, usado para descobrir os Ids gerados pelo servidor");
            return (await resposta.Content.ReadFromJsonAsync<ProcessoSeletivoDto>(JsonDoContrato.Opcoes).ConfigureAwait(false))!;
        }

        public Task<HttpResponseMessage> ObterConformidadeAsync() =>
            EnviarAsync(HttpMethod.Get, $"{Rota}/{ProcessoId}/conformidade", null, ifMatch: null, idempotente: false,
                accept: "application/vnd.uniplus.conformidade-processo-seletivo.v1+json");

        public Task<HttpResponseMessage> PublicarAsync(Guid documentoEditalId, string numero, string tipoAtoCodigo) =>
            EnviarAsync(
                HttpMethod.Post, $"{Rota}/{ProcessoId}/publicacao",
                new
                {
                    numero,
                    // O período não é mais informado: o processo do cenário tem fase que coleta
                    // inscrição, e é da janela dela que o Edital passa a declarar o prazo (#1350).
                    documentoEditalId,
                    ato = NovoAto(tipoAtoCodigo),
                },
                ifMatch: null);

        public Task<HttpResponseMessage> AbrirRetificacaoAsync(string motivo) =>
            EnviarAsync(HttpMethod.Post, $"{Rota}/{ProcessoId}/retificacao-em-curso", new { motivo }, ifMatch: null);

        public Task<HttpResponseMessage> FecharRetificacaoAsync(Guid documentoEditalId, string numero, string tipoAtoCodigo, string ifMatch) =>
            EnviarAsync(
                HttpMethod.Post, $"{Rota}/{ProcessoId}/retificacao-em-curso/fechamento",
                new
                {
                    numero,
                    // O período não é mais informado: o processo do cenário tem fase que coleta
                    // inscrição, e é da janela dela que o Edital passa a declarar o prazo (#1350).
                    documentoEditalId,
                    ato = NovoAto(tipoAtoCodigo),
                },
                ifMatch);

        public Task<HttpResponseMessage> ObterSnapshotVigenteAsync() =>
            EnviarAsync(HttpMethod.Get, $"{Rota}/{ProcessoId}/snapshot-vigente", null, ifMatch: null, idempotente: false,
                accept: "application/vnd.uniplus.snapshot-vigente-processo-seletivo.v1+json");

        private static object NovoAto(string tipoAtoCodigo) => new
        {
            orgao = "CEPS",
            serie = "EDITAL",
            ano = 2026,
            dataPublicacao = "2026-03-01",
            assinante = "Diretor do CEPS",
            tipoAtoCodigo,
        };

        private async Task<HttpResponseMessage> EnviarAsync(
            HttpMethod metodo, string rota, object? corpo, string? ifMatch, bool idempotente = true, string? accept = null)
        {
            using HttpRequestMessage request = new(metodo, new Uri(rota, UriKind.Relative));
            if (corpo is not null)
            {
                request.Content = JsonContent.Create(corpo);
            }

            // Leituras versionadas exigem o vendor media type (ADR-0028) — sem ele o servidor
            // devolve 406, e o teste "falharia" por um motivo que nada tem a ver com o que ele
            // quer provar (plano §4.0).
            if (accept is not null)
            {
                request.Headers.TryAddWithoutValidation("Accept", accept);
            }

            request.Headers.Authorization = new AuthenticationHeaderValue(
                TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
            request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");

            // Chave de idempotência NOVA em toda mutação (plano §4.0) — reutilizar transformaria
            // a segunda chamada em replay e mascararia a dimensão realmente enviada.
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
}
