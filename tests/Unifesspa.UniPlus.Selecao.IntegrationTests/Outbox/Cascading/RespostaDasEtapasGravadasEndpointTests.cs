namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

[Collection(CascadingCollection.Name)]
public sealed class RespostaDasEtapasGravadasEndpointTests(CascadingFixture fixture)
{
    [Fact]
    public async Task Definir_MesmaChave_DevolveMesmoCorpoDaLeitura()
    {
        (Guid processoId, _) = await PublicacaoPeloEndpointDeTeste.SemearProcessoAsync(fixture.Factory, nameof(RespostaDasEtapasGravadasEndpointTests));
        using HttpClient cliente = fixture.Factory.CreateClient();
        string chave = Guid.CreateVersion7().ToString("N");
        using HttpResponseMessage primeira = await EnviarAsync(cliente, processoId, chave, "plataforma-admin");
        using HttpResponseMessage repetida = await EnviarAsync(cliente, processoId, chave, "plataforma-admin");
        primeira.StatusCode.Should().Be(HttpStatusCode.OK);
        repetida.StatusCode.Should().Be(HttpStatusCode.OK);
        string corpo = await primeira.Content.ReadAsStringAsync();
        (await repetida.Content.ReadAsStringAsync()).Should().Be(corpo);
        JsonNode? leitura = await LerEtapasAsync(cliente, processoId);
        JsonNode.DeepEquals(JsonNode.Parse(corpo), leitura).Should().BeTrue();
    }

    [Fact]
    public async Task Definir_BancaNova_DevolveIdentidadeEValoresGravados()
    {
        (Guid processoId, _) = await PublicacaoPeloEndpointDeTeste.SemearProcessoAsync(fixture.Factory, nameof(Definir_BancaNova_DevolveIdentidadeEValoresGravados));
        CatalogosDoEnvelopeFechado.CatalogosSemeados catalogos = await CatalogosDoEnvelopeFechado.SemearCatalogosRicosAsync(fixture.Factory, Guid.CreateVersion7().ToString("N")[..8]);
        using HttpClient cliente = fixture.Factory.CreateClient();
        JsonArray etapas = EtapasComBanca(catalogos.TipoBancaId);
        using HttpResponseMessage resposta = await EnviarEtapasAsync(cliente, processoId, etapas);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        JsonNode corpo = JsonNode.Parse(await resposta.Content.ReadAsStringAsync())!;
        JsonNode banca = corpo[1]!["bancas"]![0]!;
        Guid.Parse(banca["id"]!.GetValue<string>()).Should().NotBeEmpty();
        banca["tipoBancaOrigemId"]!.GetValue<Guid>().Should().Be(catalogos.TipoBancaId);
        banca["posicaoNoTipo"]!.GetValue<int>().Should().Be(1);
        JsonNode.DeepEquals(corpo, await LerEtapasAsync(cliente, processoId)).Should().BeTrue();
    }

    [Fact]
    public async Task Definir_DataComSeteCasas_DevolveOMesmoCorpoDaLeitura()
    {
        (Guid processoId, _) = await PublicacaoPeloEndpointDeTeste.SemearProcessoAsync(fixture.Factory, nameof(Definir_DataComSeteCasas_DevolveOMesmoCorpoDaLeitura));
        CatalogosDoEnvelopeFechado.CatalogosSemeados catalogos = await CatalogosDoEnvelopeFechado.SemearCatalogosRicosAsync(fixture.Factory, Guid.CreateVersion7().ToString("N")[^8..]);
        using HttpClient cliente = fixture.Factory.CreateClient();
        JsonArray etapas = EtapasComBanca(catalogos.TipoBancaId);
        etapas[0]!["inicio"] = "2026-01-25T11:00:00.1234567Z";
        etapas[0]!["fim"] = "2026-01-27T11:00:00.7654321Z";
        using HttpResponseMessage resposta = await EnviarEtapasAsync(cliente, processoId, etapas);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        JsonNode corpo = JsonNode.Parse(await resposta.Content.ReadAsStringAsync())!;
        corpo[0]!["janela"]!["inicio"]!.GetValue<DateTimeOffset>()
            .Should().Be(new DateTimeOffset(2026, 1, 25, 11, 0, 0, TimeSpan.Zero).AddTicks(1_234_560));
        JsonNode.DeepEquals(corpo, await LerEtapasAsync(cliente, processoId)).Should().BeTrue();
    }

    [Fact]
    public async Task Definir_DecimaisComQuatroCasas_DevolveOMesmoCorpoDaLeitura()
    {
        (Guid processoId, _) = await PublicacaoPeloEndpointDeTeste.SemearProcessoAsync(fixture.Factory, nameof(Definir_DecimaisComQuatroCasas_DevolveOMesmoCorpoDaLeitura));
        CatalogosDoEnvelopeFechado.CatalogosSemeados catalogos = await CatalogosDoEnvelopeFechado.SemearCatalogosRicosAsync(fixture.Factory, Guid.CreateVersion7().ToString("N")[^8..]);
        using HttpClient cliente = fixture.Factory.CreateClient();
        JsonArray etapas = EtapasComBanca(catalogos.TipoBancaId);
        etapas[0]!["peso"] = 1.1234m;
        etapas[0]!["notaMinima"] = 40.1234m;
        etapas[0]!["notaMaxima"] = 100.1234m;
        etapas[0]!["itensDeAvaliacao"] = JsonSerializer.SerializeToNode(new[] { new { rotulo = "Item", pontuacaoMaxima = 1.1234m } });
        etapas[1]!["notaMaxima"] = 0.0001m;
        etapas[1]!["passoDoLancamento"] = 0.0001m;
        etapas[0]!["peso"] = 1.12345m;
        using (HttpResponseMessage invalida = await EnviarEtapasAsync(cliente, processoId, etapas))
        {
            invalida.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await invalida.Content.ReadAsStringAsync()).Should().Contain("uniplus.validacao.formato");
        }
        etapas[0]!["peso"] = 1.1234m;
        using HttpResponseMessage resposta = await EnviarEtapasAsync(cliente, processoId, etapas);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        JsonNode corpo = JsonNode.Parse(await resposta.Content.ReadAsStringAsync())!;
        corpo[0]!["nota"]!["peso"]!.GetValue<decimal>().Should().Be(1.1234m);
        corpo[0]!["nota"]!["notaMinima"]!.GetValue<decimal>().Should().Be(40.1234m);
        corpo[0]!["nota"]!["notaMaxima"]!.GetValue<decimal>().Should().Be(100.1234m);
        corpo[0]!["avaliacao"]!["itensDeAvaliacao"]![0]!["pontuacaoMaxima"]!.GetValue<decimal>().Should().Be(1.1234m);
        corpo[1]!["nota"]!["passoDoLancamento"]!.GetValue<decimal>().Should().Be(0.0001m);
        JsonNode leitura = (await LerEtapasAsync(cliente, processoId))!;
        leitura[0]!["nota"]!["peso"]!.GetValue<decimal>().Should().Be(corpo[0]!["nota"]!["peso"]!.GetValue<decimal>());
        leitura[0]!["nota"]!["notaMinima"]!.GetValue<decimal>().Should().Be(corpo[0]!["nota"]!["notaMinima"]!.GetValue<decimal>());
        leitura[0]!["nota"]!["notaMaxima"]!.GetValue<decimal>().Should().Be(corpo[0]!["nota"]!["notaMaxima"]!.GetValue<decimal>());
        leitura[0]!["avaliacao"]!["itensDeAvaliacao"]![0]!["pontuacaoMaxima"]!.GetValue<decimal>().Should().Be(1.1234m);
        leitura[1]!["nota"]!["passoDoLancamento"]!.GetValue<decimal>().Should().Be(0.0001m);
    }

    [Fact]
    public async Task Definir_PesoComCincoCasasENomeVazio_AcumulaRecusas()
    {
        (Guid processoId, _) = await PublicacaoPeloEndpointDeTeste.SemearProcessoAsync(fixture.Factory, nameof(Definir_PesoComCincoCasasENomeVazio_AcumulaRecusas));
        CatalogosDoEnvelopeFechado.CatalogosSemeados catalogos = await CatalogosDoEnvelopeFechado.SemearCatalogosRicosAsync(fixture.Factory, Guid.CreateVersion7().ToString("N")[^8..]);
        using HttpClient cliente = fixture.Factory.CreateClient();
        JsonArray etapas = EtapasComBanca(catalogos.TipoBancaId);
        etapas[0]!["peso"] = 1.12345m;
        etapas[0]!["nome"] = "";
        using HttpResponseMessage resposta = await EnviarEtapasAsync(cliente, processoId, etapas);
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, corpo);
        corpo.Should().Contain("uniplus.validacao.formato").And.Contain("uniplus.selecao.etapa_processo.nome_obrigatorio");
    }

    [Fact]
    public async Task Definir_PrazosDoRecursoComQuatroCasas_DevolveOMesmoCorpoDaLeitura()
    {
        (Guid processoId, _) = await PublicacaoPeloEndpointDeTeste.SemearProcessoAsync(fixture.Factory, nameof(Definir_PrazosDoRecursoComQuatroCasas_DevolveOMesmoCorpoDaLeitura));
        await CatalogosDoEnvelopeFechado.SemearTipoDeAtoResultadoPreliminarAsync(fixture.Factory);
        CatalogosDoEnvelopeFechado.CatalogosSemeados catalogos = await CatalogosDoEnvelopeFechado.SemearCatalogosRicosAsync(fixture.Factory, Guid.CreateVersion7().ToString("N")[^8..]);
        using HttpClient cliente = fixture.Factory.CreateClient();
        JsonArray etapas = EtapasComBanca(catalogos.TipoBancaId);
        etapas[0]!["produtos"] = JsonSerializer.SerializeToNode(PayloadsDoEnvelopeFechado.ProdutosDoResultado());
        etapas[0]!["recursos"] = JsonSerializer.SerializeToNode(new[] { new
        {
            ancora = 1, regraCodigo = RegraPrazoRecursoCodigo.AncoradoEmAto, regraVersao = "v1",
            prazoValor = 1.1m, prazoUnidade = "horas", atoAncoraCodigo = CheckpointsDoEnvelopeFechado.CodigoAtoResultado,
            admiteInstanciaSuperior = true, prazoSegundaInstanciaValor = 2.1234m,
            prazoSegundaInstanciaUnidade = "horas", efeitoSuspensivo = "semEfeitoSuspensivo",
        } });
        etapas[0]!["recursos"]![0]!["prazoValor"] = 1.12345m;
        using (HttpResponseMessage invalida = await EnviarEtapasAsync(cliente, processoId, etapas))
        {
            invalida.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await invalida.Content.ReadAsStringAsync()).Should().Contain("uniplus.validacao.formato");
        }
        etapas[0]!["recursos"]![0]!["prazoValor"] = 1.1m;
        using HttpResponseMessage resposta = await EnviarEtapasAsync(cliente, processoId, etapas);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        JsonNode corpo = JsonNode.Parse(await resposta.Content.ReadAsStringAsync())!;
        JsonNode parametros = corpo[0]!["publicacoes"]!["recursos"]![0]!["parametros"]!;
        parametros["prazoValor"]!.GetValue<decimal>().Should().Be(1.1m);
        parametros["prazoSegundaInstanciaValor"]!.GetValue<decimal>().Should().Be(2.1234m);
        JsonNode leitura = (await LerEtapasAsync(cliente, processoId))!;
        JsonNode parametrosLidos = leitura[0]!["publicacoes"]!["recursos"]![0]!["parametros"]!;
        parametrosLidos["prazoValor"]!.GetValue<decimal>().Should().Be(parametros["prazoValor"]!.GetValue<decimal>());
        parametrosLidos["prazoSegundaInstanciaValor"]!.GetValue<decimal>().Should().Be(parametros["prazoSegundaInstanciaValor"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task Definir_EscalaDeAtendimento_DevolveMesmosNumerosNaLeitura()
    {
        (Guid processoId, _) = await PublicacaoPeloEndpointDeTeste.SemearProcessoAsync(fixture.Factory, nameof(Definir_EscalaDeAtendimento_DevolveMesmosNumerosNaLeitura));
        CatalogosDoEnvelopeFechado.CatalogosSemeados catalogos = await CatalogosDoEnvelopeFechado.SemearCatalogosRicosAsync(fixture.Factory, Guid.CreateVersion7().ToString("N")[^8..]);
        using HttpClient cliente = fixture.Factory.CreateClient();
        JsonArray etapas = EtapasComBanca(catalogos.TipoBancaId);
        etapas[1]!["passoDoLancamento"] = null;
        etapas[1]!["itensDeAvaliacao"] = JsonSerializer.SerializeToNode(new[] { new { rotulo = "Item", pontuacaoMaxima = 100m } });
        etapas[1]!["escalaDeAtendimento"] = JsonSerializer.SerializeToNode(new[] { new { rotulo = "Completo", fracao = 1m }, new { rotulo = "Parcial", fracao = 0.25m } });
        using HttpResponseMessage resposta = await EnviarEtapasAsync(cliente, processoId, etapas);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        JsonNode corpo = JsonNode.Parse(await resposta.Content.ReadAsStringAsync())!;
        JsonNode leitura = (await LerEtapasAsync(cliente, processoId))!;
        JsonArray escala = corpo[1]!["avaliacao"]!["escalaDeAtendimento"]!.AsArray();
        JsonArray escalaLida = leitura[1]!["avaliacao"]!["escalaDeAtendimento"]!.AsArray();
        escala[1]!["fracao"]!.GetValue<decimal>().Should().Be(0.25m);
        escalaLida[1]!["fracao"]!.GetValue<decimal>().Should().Be(0.25m);
        for (int i = 0; i < escala.Count; i++)
        {
            escala[i]!["fracao"]!.GetValue<decimal>().Should().Be(escalaLida[i]!["fracao"]!.GetValue<decimal>());
        }
    }

    [Fact]
    public async Task Definir_FracaoForaDoIntervalo_RecusaDoDominioCom422()
    {
        (Guid processoId, _) = await PublicacaoPeloEndpointDeTeste.SemearProcessoAsync(fixture.Factory, nameof(Definir_FracaoForaDoIntervalo_RecusaDoDominioCom422));
        CatalogosDoEnvelopeFechado.CatalogosSemeados catalogos = await CatalogosDoEnvelopeFechado.SemearCatalogosRicosAsync(fixture.Factory, Guid.CreateVersion7().ToString("N")[^8..]);
        using HttpClient cliente = fixture.Factory.CreateClient();
        JsonArray etapas = EtapasComBanca(catalogos.TipoBancaId);
        etapas[0]!["escalaDeAtendimento"] = JsonSerializer.SerializeToNode(new[] { new { rotulo = "Completo", fracao = 1m }, new { rotulo = "Inválido", fracao = 1.5m } });
        using HttpResponseMessage resposta = await EnviarEtapasAsync(cliente, processoId, etapas);
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, corpo);
        corpo.Should().Contain("uniplus.validacao.formato").And.Contain("A fração do nível deve ter no máximo 4 casas decimais e ficar entre 0 e 1.");
    }

    private static JsonArray EtapasComBanca(Guid tipoBancaId)
    {
        JsonArray etapas = JsonSerializer.SerializeToNode(PayloadsDoEnvelopeFechado.EtapasIniciais(tipoBancaId))!.AsArray();
        foreach (JsonNode? etapa in etapas)
        {
            etapa!["faseCodigo"] = "RESULTADO_FINAL";
        }

        return etapas;
    }

    private static async Task<HttpResponseMessage> EnviarEtapasAsync(HttpClient cliente, Guid processoId, JsonArray etapas)
    {
        using HttpRequestMessage pedido = new(HttpMethod.Put, new Uri($"/api/selecao/processos-seletivos/{processoId}/etapas", UriKind.Relative))
        {
            Content = JsonContent.Create(etapas),
        };
        Autenticar(pedido, "plataforma-admin");
        pedido.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await cliente.SendAsync(pedido);
    }

    [Fact]
    public async Task Definir_SemPermissao_RecusaSemDadosDoProcesso()
    {
        (Guid processoId, _) = await PublicacaoPeloEndpointDeTeste.SemearProcessoAsync(fixture.Factory, nameof(Definir_SemPermissao_RecusaSemDadosDoProcesso));
        using HttpClient cliente = fixture.Factory.CreateClient();
        using HttpResponseMessage resposta = await EnviarAsync(cliente, processoId, Guid.CreateVersion7().ToString("N"), "candidato");
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await resposta.Content.ReadAsStringAsync()).Should().NotContain("etapas");
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, Guid processoId, string chave, string papel)
    {
        using HttpRequestMessage pedido = new(HttpMethod.Put, new Uri($"/api/selecao/processos-seletivos/{processoId}/etapas", UriKind.Relative))
        {
            Content = JsonContent.Create(Array.Empty<object>()),
        };
        Autenticar(pedido, papel);
        pedido.Headers.TryAddWithoutValidation("Idempotency-Key", chave);
        return await cliente.SendAsync(pedido);
    }

    private static async Task<JsonNode?> LerEtapasAsync(HttpClient cliente, Guid processoId)
    {
        using HttpRequestMessage pedido = new(HttpMethod.Get, new Uri($"/api/selecao/processos-seletivos/{processoId}", UriKind.Relative));
        Autenticar(pedido, "plataforma-admin");
        using HttpResponseMessage resposta = await cliente.SendAsync(pedido);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonNode? processo = JsonNode.Parse(await resposta.Content.ReadAsStringAsync());
        return processo!["etapas"];
    }

    private static void Autenticar(HttpRequestMessage pedido, string papel)
    {
        pedido.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        pedido.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, papel);
    }
}
