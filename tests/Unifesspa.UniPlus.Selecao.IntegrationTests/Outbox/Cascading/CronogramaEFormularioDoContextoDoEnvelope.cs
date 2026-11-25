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

internal static class CronogramaEFormularioDoContextoDoEnvelope
{
    public static Task<HttpResponseMessage> PutCronogramaFasesAsync(this ContextoDoEnvelopeFechado contexto) =>
        contexto.EnviarAsync(HttpMethod.Put, $"{ContextoDoEnvelopeFechado.Rota}/{contexto.ProcessoId}/cronograma-fases", CronogramaDeFases(contexto.Catalogos));

    public static Task<HttpResponseMessage> PutFatosColetadosAsync(this ContextoDoEnvelopeFechado contexto) =>
        contexto.EnviarAsync(
            HttpMethod.Put, FormularioDeInscricaoHttp.RotaDosItens(contexto.ProcessoId).ToString(),
            FormularioDeInscricaoHttp.CorpoDosItens(FatosColetados()));

    public static Task<HttpResponseMessage> PutRegrasDerivacaoAsync(this ContextoDoEnvelopeFechado contexto) =>
        contexto.EnviarAsync(HttpMethod.Put, $"{ContextoDoEnvelopeFechado.Rota}/{contexto.ProcessoId}/regras-derivacao", RegrasDeDerivacao());

    public static Task<HttpResponseMessage> PutCascataRemanejamentoAsync(this ContextoDoEnvelopeFechado contexto) =>
        contexto.EnviarAsync(
            HttpMethod.Put, $"{ContextoDoEnvelopeFechado.Rota}/{contexto.ProcessoId}/cascata-remanejamento", Cascata());

    public static Task<HttpResponseMessage> PutDocumentosExigidosAsync(this ContextoDoEnvelopeFechado contexto, object[] raizes) =>
        contexto.EnviarAsync(HttpMethod.Put, $"{ContextoDoEnvelopeFechado.Rota}/{contexto.ProcessoId}/documentos-exigidos", raizes);

    public static Task<HttpResponseMessage> PutFormularioAsync(this ContextoDoEnvelopeFechado contexto, string titulo, Guid faseId) =>
        contexto.EnviarAsync(
            HttpMethod.Put, $"/api/selecao/admin/processos-seletivos/{contexto.ProcessoId}/formularios/INSCRICAO",
            new
            {
                faseId,
                titulo,
                paginas = new object[]
                {
                    new { codigo = "DADOS", ordem = 0, tipo = "SECAO", bloco = (string?)null, apresentacao = new { titulo = "Dados" } },
                    new { codigo = "REVISAO", ordem = 1, tipo = "BLOCO", bloco = "REVISAO_E_ACEITE", apresentacao = new { titulo = "Revisão e aceite" } },
                },
            });

    public static Task<HttpResponseMessage> PutDivulgacaoAsync(this ContextoDoEnvelopeFechado contexto, IReadOnlyList<string>? camposPublicos, string? justificativa) =>
        contexto.EnviarAsync(
            HttpMethod.Put, $"{ContextoDoEnvelopeFechado.Rota}/{contexto.ProcessoId}/divulgacao",
            new { camposPublicos, justificativa });
}
