namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.DTOs;

using static PassosDoEnvelopeFechado;

[Collection(CascadingCollection.Name)]
public sealed class OrdemDasEtapasEndpointTests(CascadingFixture ambienteDeTeste)
{
    [Fact]
    public async Task DefinirEtapas_OrdensTrocadas_MantemIdentificadoresEChaves()
    {
        (Cenario cenario, JsonArray etapas, EtapaProcessoDto[] anteriores) = await PrepararEtapasAsync();
        etapas[0]!["ordem"] = 2;
        etapas[1]!["ordem"] = 1;
        using HttpResponseMessage resposta = await EnviarAsync(cenario, etapas);
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
        EtapaProcessoDto[] posteriores = [.. (await cenario.Ctx.ObterProcessoAsync()).Etapas];
        ConferirEtapa(posteriores, anteriores[0], 2);
        ConferirEtapa(posteriores, anteriores[1], 1);
    }

    [Fact]
    public async Task DefinirEtapas_OrdensRepetidas_RecusaSemAlterarAsEtapas()
    {
        (Cenario cenario, JsonArray etapas, EtapaProcessoDto[] anteriores) = await PrepararEtapasAsync();
        etapas[1]!["ordem"] = 1;
        using HttpResponseMessage resposta = await EnviarAsync(cenario, etapas);
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await resposta.Content.ReadAsStringAsync());
        string corpo = await resposta.Content.ReadAsStringAsync();
        corpo.Should().Contain("uniplus.selecao.processo_seletivo.ordem_etapa_duplicada");
        EtapaProcessoDto[] posteriores = [.. (await cenario.Ctx.ObterProcessoAsync()).Etapas];
        ConferirEtapa(posteriores, anteriores[0], 1);
        ConferirEtapa(posteriores, anteriores[1], 2);
    }

    private async Task<(Cenario Cenario, JsonArray Etapas, EtapaProcessoDto[] Anteriores)> PrepararEtapasAsync()
    {
        Cenario cenario = await PrepararAsync(ambienteDeTeste.Factory);
        await CriarProcessoEmRascunhoAsync(cenario);
        JsonArray etapas = CriarDuasEtapas(cenario);
        using HttpResponseMessage resposta = await EnviarAsync(cenario, etapas);
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
        EtapaProcessoDto[] anteriores = [.. (await cenario.Ctx.ObterProcessoAsync()).Etapas.OrderBy(e => e.Identificacao.Ordem)];
        anteriores.Should().HaveCount(2);
        etapas[0]!["id"] = anteriores[0].Id;
        etapas[1]!["id"] = anteriores[1].Id;
        return (cenario, etapas, anteriores);
    }

    private static JsonArray CriarDuasEtapas(Cenario cenario)
    {
        JsonArray etapas = JsonSerializer.SerializeToNode(PayloadsDoEnvelopeFechado.EtapasIniciais(cenario.Ctx.Catalogos.TipoBancaId))!.AsArray();
        JsonObject primeira = etapas[0]!.DeepClone().AsObject();
        primeira["faseCodigo"] = null;
        JsonObject segunda = primeira.DeepClone().AsObject();
        segunda["nome"] = "Segunda prova";
        segunda["ordem"] = 2;
        return new JsonArray(primeira, segunda);
    }

    private static Task<HttpResponseMessage> EnviarAsync(Cenario cenario, JsonArray etapas) =>
        cenario.Ctx.PutEtapasAsync([.. etapas.Select(etapa => (object)etapa!)]);

    private static void ConferirEtapa(EtapaProcessoDto[] posteriores, EtapaProcessoDto anterior, int ordem)
    {
        EtapaProcessoDto etapa = posteriores.Should().ContainSingle(e => e.Id == anterior.Id).Subject;
        etapa.Chave.Should().Be(anterior.Chave);
        etapa.Identificacao.Ordem.Should().Be(ordem);
    }
}
