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

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.CatalogosDoEnvelopeFechado;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.CheckpointsDoEnvelopeFechado;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ItensDoChecklistDoEnvelopeFechado;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.PassosDoEnvelopeFechado;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.PayloadsDoEnvelopeFechado;

internal static class PublicacaoDoContextoDoEnvelope
{
    public static async Task<ProcessoSeletivoDto> ObterProcessoAsync(this ContextoDoEnvelopeFechado contexto)
    {
        HttpResponseMessage resposta = await contexto.EnviarAsync(
            HttpMethod.Get, $"{ContextoDoEnvelopeFechado.Rota}/{contexto.ProcessoId}", null, ifMatch: null, idempotente: false,
            accept: "application/vnd.uniplus.processo-seletivo.v1+json").ConfigureAwait(false);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, "GET do recurso, usado para descobrir os Ids gerados pelo servidor");
        return (await resposta.Content.ReadFromJsonAsync<ProcessoSeletivoDto>(JsonDoContrato.Opcoes).ConfigureAwait(false))!;
    }


    public static Task<HttpResponseMessage> ObterConformidadeAsync(this ContextoDoEnvelopeFechado contexto) =>
        contexto.EnviarAsync(HttpMethod.Get, $"{ContextoDoEnvelopeFechado.Rota}/{contexto.ProcessoId}/conformidade", null, ifMatch: null, idempotente: false,
            accept: "application/vnd.uniplus.conformidade-processo-seletivo.v1+json");


    public static Task<HttpResponseMessage> PublicarAsync(this ContextoDoEnvelopeFechado contexto, Guid documentoEditalId, string numero, string tipoAtoCodigo) =>
        contexto.EnviarAsync(
            HttpMethod.Post, $"{ContextoDoEnvelopeFechado.Rota}/{contexto.ProcessoId}/publicacao",
            new
            {
                numero,
                // O período não é mais informado: o processo do cenário tem fase que coleta
                // inscrição, e é da janela dela que o Edital passa a declarar o prazo (#1350).
                documentoEditalId,
                ato = NovoAto(tipoAtoCodigo),
            },
            ifMatch: null);


    public static Task<HttpResponseMessage> AbrirRetificacaoAsync(this ContextoDoEnvelopeFechado contexto, string motivo) =>
        contexto.EnviarAsync(HttpMethod.Post, $"{ContextoDoEnvelopeFechado.Rota}/{contexto.ProcessoId}/retificacao-em-curso", new { motivo }, ifMatch: null);


    public static Task<HttpResponseMessage> FecharRetificacaoAsync(this ContextoDoEnvelopeFechado contexto, Guid documentoEditalId, string numero, string tipoAtoCodigo, string ifMatch) =>
        contexto.EnviarAsync(
            HttpMethod.Post, $"{ContextoDoEnvelopeFechado.Rota}/{contexto.ProcessoId}/retificacao-em-curso/fechamento",
            new
            {
                numero,
                // O período não é mais informado: o processo do cenário tem fase que coleta
                // inscrição, e é da janela dela que o Edital passa a declarar o prazo (#1350).
                documentoEditalId,
                ato = NovoAto(tipoAtoCodigo),
            },
            ifMatch);


    public static Task<HttpResponseMessage> ObterSnapshotVigenteAsync(this ContextoDoEnvelopeFechado contexto) =>
        contexto.EnviarAsync(HttpMethod.Get, $"{ContextoDoEnvelopeFechado.Rota}/{contexto.ProcessoId}/snapshot-vigente", null, ifMatch: null, idempotente: false,
            accept: "application/vnd.uniplus.snapshot-vigente-processo-seletivo.v1+json");


    internal static object NovoAto(string tipoAtoCodigo) => new
    {
        orgao = "CEPS",
        serie = "EDITAL",
        ano = 2026,
        dataDoAto = "2026-03-01",
        assinante = "Diretor do CEPS",
        tipoAtoCodigo,
    };

}
