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

internal static class CheckpointsDoEnvelopeFechado
{
    /// <summary>
    /// Os códigos dos atos que a fase de resultado publica — o preliminar, que abre o ciclo
    /// recursal, e o definitivo, que o conclui. Nenhum congela configuração (são resultado,
    /// não edital/retificação) e nenhum é único por objeto (um certame tem mais de um
    /// resultado ao longo do ciclo).
    /// </summary>
    internal const string CodigoAtoResultado = "RESULTADO_CLASSIFICACAO";

    // ══════════════════════════════════════════════════════════════════════════════
    // Checkpoint — valida a resposta IMEDIATAMENTE, nomeando fase/rota/status/ProblemDetails
    // ══════════════════════════════════════════════════════════════════════════════

    internal static async Task<HttpResponseMessage> ExecutarPassoAsync(
        Func<Task<HttpResponseMessage>> acao, HttpStatusCode esperado, string passo, string rota)
    {
        HttpResponseMessage resposta = await acao().ConfigureAwait(false);
        if (resposta.StatusCode != esperado)
        {
            string corpo = await resposta.Content.ReadAsStringAsync().ConfigureAwait(false);
            resposta.StatusCode.Should().Be(
                esperado,
                $"passo '{passo}' ({rota}) esperava {(int)esperado} {esperado} e recebeu " +
                $"{(int)resposta.StatusCode} {resposta.StatusCode}. ProblemDetails: {corpo}");
        }

        return resposta;
    }

    internal static string LerETag(HttpResponseMessage resposta, string passo)
    {
        resposta.Headers.ETag.Should().NotBeNull($"passo '{passo}': a resposta de uma sessão editorial carrega o ETag");
        return resposta.Headers.ETag!.ToString();
    }

    /// <summary>
    /// Localiza, dentro do bloco <c>etapas</c> do envelope congelado, o item cujo <c>id</c>
    /// bate com <paramref name="etapaId"/> — casamento por identidade, nunca por posição no
    /// array (<c>SerializacaoDasEtapas.SerializarEtapas</c> reordena por conteúdo
    /// quando duas etapas empatam em <c>Ordem</c>).
    /// </summary>
    internal static JsonObject LocalizarEtapaPorId(JsonNode configuracao, Guid etapaId) =>
        configuracao["etapas"]!.AsArray()
            .Select(static no => no!.AsObject())
            .Single(etapa => etapa["id"]!.GetValue<Guid>() == etapaId);
}
