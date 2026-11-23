namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

using static PassosDoEnvelopeFechado;

[Collection(CascadingCollection.Name)]
public sealed class OrdemDosNiveisEndpointTests(CascadingFixture ambienteDeTeste)
{
    [Fact]
    public async Task DefinirNiveis_ConsultaEEnvelope_MantemMesmaOrdem()
    {
        Cenario cenario = await PrepararAsync(ambienteDeTeste.Factory);
        await ConfigurarTodasAsDimensoesAsync(cenario);
        string[] rotulos = await DefinirEscalaAsync(cenario);
        EtapaProcessoDto consultada = (await cenario.Ctx.ObterProcessoAsync()).Etapas.Single(e => e.Id == cenario.RedacaoId);
        await PublicarV1Async(cenario);
        string[] congelados = await LerNiveisCongeladosAsync(cenario);
        rotulos.Should().Equal("Não atende", "Completo", "Parcial");
        consultada.Avaliacao.EscalaDeAtendimento!.Select(n => n.Rotulo).Should().Equal(rotulos);
        consultada.Avaliacao.EscalaDeAtendimento!.Select(n => n.Chave).Should().Equal("nivel_nao_atende", "nivel_completo", "nivel_parcial");
        consultada.Avaliacao.ItensDeAvaliacao.Single().Chave.Should().Be("item");
        congelados.Should().Equal(rotulos);
    }

    private static async Task<string[]> DefinirEscalaAsync(Cenario cenario)
    {
        JsonArray etapas = EtapasComEscala(cenario);
        using HttpResponseMessage resposta = await cenario.Ctx.PutEtapasAsync([.. etapas.Select(e => (object)e!)]);
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
        EtapaProcessoDto etapa = (await cenario.Ctx.ObterProcessoAsync()).Etapas.Single(e => e.Id == cenario.RedacaoId);
        return [.. etapa.Avaliacao.EscalaDeAtendimento!.Select(n => n.Rotulo)];
    }

    private static JsonArray EtapasComEscala(Cenario cenario)
    {
        JsonArray etapas = JsonSerializer.SerializeToNode(PayloadsDoEnvelopeFechado.EtapasIniciais(cenario.Ctx.Catalogos.TipoBancaId))!.AsArray();
        etapas[0]!["id"] = cenario.ObjetivaId;
        etapas[1]!["id"] = cenario.RedacaoId;
        etapas[2]!["id"] = cenario.EntrevistaId;
        etapas[1]!["passoDoLancamento"] = null;
        etapas[1]!["itensDeAvaliacao"] = JsonSerializer.SerializeToNode(new[] { new { rotulo = "Item", pontuacaoMaxima = 100m } });
        etapas[1]!["escalaDeAtendimento"] = NiveisDeclarados();
        return etapas;
    }

    private static JsonNode? NiveisDeclarados() => JsonSerializer.SerializeToNode(new[]
    {
        new { rotulo = "Não atende", fracao = 0m },
        new { rotulo = "Completo", fracao = 1m },
        new { rotulo = "Parcial", fracao = 0.5m },
    });

    private static async Task<string[]> LerNiveisCongeladosAsync(Cenario cenario)
    {
        await using AsyncServiceScope escopo = cenario.Api.Services.CreateAsyncScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        VersaoConfiguracao versao = await contexto.Set<VersaoConfiguracao>().SingleAsync(v => v.ProcessoSeletivoId == cenario.Ctx.ProcessoId);
        JsonNode envelope = JsonNode.Parse(versao.ConfiguracaoCongelada)!;
        JsonNode etapa = envelope["etapas"]!.AsArray().Single(e => e!["nome"]!.GetValue<string>() == "Redação")!;
        ConferirChavesCongeladas(etapa);
        return [.. etapa["escalaDeAtendimento"]!.AsArray().Select(n => n!["rotulo"]!.GetValue<string>())];
    }
    private static void ConferirChavesCongeladas(JsonNode etapa)
    {
        etapa["itensDeAvaliacao"]![0]!["chave"]!.GetValue<string>().Should().Be("item");
        etapa["escalaDeAtendimento"]!.AsArray().Select(n => n!["chave"]!.GetValue<string>())
            .Should().Equal("nivel_nao_atende", "nivel_completo", "nivel_parcial");
    }
}
