namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDoCronogramaDeFasesPeloEndpoint;

[Collection(CascadingCollection.Name)]
public sealed class GravacaoDasEtapasEndpointTests(CascadingFixture ambienteDeTeste)
{
    [Fact]
    public async Task Definir_MesmaChave_DevolveARespostaGuardada()
    {
        (Guid processoId, _) = await PrepararAsync();
        using HttpClient cliente = ambienteDeTeste.Factory.CreateClient();
        string chave = Guid.CreateVersion7().ToString("N");
        using HttpResponseMessage primeira = await EnviarAsync(cliente, processoId, new JsonArray(), chave, "plataforma-admin");
        using HttpResponseMessage repetida = await EnviarAsync(cliente, processoId, new JsonArray(), chave, "plataforma-admin");
        primeira.StatusCode.Should().Be(HttpStatusCode.NoContent, await primeira.Content.ReadAsStringAsync());
        repetida.StatusCode.Should().Be(HttpStatusCode.NoContent, await repetida.Content.ReadAsStringAsync());
        repetida.Headers.GetValues("Idempotency-Replayed").Should().ContainSingle().Which.Should().Be("true");
        (await LerProcessoComoJsonAsync(cliente, processoId))["etapas"]!.AsArray().Should().BeEmpty();
    }

    [Fact]
    public async Task Definir_BancaNova_GravaIdentidadeCopiaDoTipoEPosicao()
    {
        (Guid processoId, Guid tipoBancaId) = await PrepararAsync();
        using HttpClient cliente = ambienteDeTeste.Factory.CreateClient();
        JsonNode etapas = await GravarELerEtapasAsync(cliente, processoId, EtapasComBanca(tipoBancaId));
        JsonNode banca = etapas[1]!["bancas"]![0]!;
        banca["id"]!.GetValue<Guid>().Should().NotBeEmpty();
        banca["tipoBancaOrigemId"]!.GetValue<Guid>().Should().Be(tipoBancaId);
        banca["posicaoNoTipo"]!.GetValue<int>().Should().Be(1);
    }

    [Fact]
    public async Task Definir_DataComSeteCasas_GravaComPrecisaoDeMicrossegundo()
    {
        (Guid processoId, Guid tipoBancaId) = await PrepararAsync();
        using HttpClient cliente = ambienteDeTeste.Factory.CreateClient();
        JsonArray etapas = EtapasComBanca(tipoBancaId);
        etapas[0]!["inicio"] = "2026-01-25T11:00:00.1234567Z";
        etapas[0]!["fim"] = "2026-01-27T11:00:00.7654321Z";
        JsonNode leitura = await GravarELerEtapasAsync(cliente, processoId, etapas);
        leitura[0]!["janela"]!["inicio"]!.GetValue<DateTimeOffset>()
            .Should().Be(new DateTimeOffset(2026, 1, 25, 11, 0, 0, TimeSpan.Zero).AddTicks(1_234_560));
    }

    [Fact]
    public async Task Definir_DecimaisComQuatroCasas_GravaOsValoresDeclarados()
    {
        (Guid processoId, Guid tipoBancaId) = await PrepararAsync();
        using HttpClient cliente = ambienteDeTeste.Factory.CreateClient();
        JsonNode leitura = await GravarELerEtapasAsync(cliente, processoId, EtapasComDecimaisDeQuatroCasas(tipoBancaId));
        leitura[0]!["nota"]!["peso"]!.GetValue<decimal>().Should().Be(1.1234m);
        leitura[0]!["nota"]!["notaMinima"]!.GetValue<decimal>().Should().Be(40.1234m);
        leitura[0]!["nota"]!["notaMaxima"]!.GetValue<decimal>().Should().Be(100.1234m);
        leitura[0]!["avaliacao"]!["itensDeAvaliacao"]![0]!["pontuacaoMaxima"]!.GetValue<decimal>().Should().Be(1.1234m);
        leitura[1]!["nota"]!["passoDoLancamento"]!.GetValue<decimal>().Should().Be(0.0001m);
    }

    [Fact]
    public async Task Definir_PrazoDoRecursoComCincoCasas_RecusaPorFormato()
    {
        (Guid processoId, Guid tipoBancaId) = await PrepararAsync();
        using HttpClient cliente = ambienteDeTeste.Factory.CreateClient();
        JsonArray etapas = EtapasComRecurso(tipoBancaId);
        etapas[0]!["recursos"]![0]!["prazoValor"] = 1.12345m;
        using HttpResponseMessage resposta = await EnviarEtapasAsync(cliente, processoId, etapas);
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("uniplus.validacao.formato");
    }

    [Fact]
    public async Task Definir_PrazosDoRecursoComQuatroCasas_GravaOsValoresDeclarados()
    {
        (Guid processoId, Guid tipoBancaId) = await PrepararAsync();
        using HttpClient cliente = ambienteDeTeste.Factory.CreateClient();
        JsonNode leitura = await GravarELerEtapasAsync(cliente, processoId, EtapasComRecurso(tipoBancaId));
        JsonNode parametros = leitura[0]!["publicacoes"]!["recursos"]![0]!["parametros"]!;
        parametros["prazoValor"]!.GetValue<decimal>().Should().Be(1.1m);
        parametros["prazoSegundaInstanciaValor"]!.GetValue<decimal>().Should().Be(2.1234m);
    }

    [Fact]
    public async Task Definir_EscalaDeAtendimento_GravaAsFracoesDeclaradas()
    {
        (Guid processoId, Guid tipoBancaId) = await PrepararAsync();
        using HttpClient cliente = ambienteDeTeste.Factory.CreateClient();
        JsonArray etapas = EtapasComBanca(tipoBancaId);
        etapas[1]!["passoDoLancamento"] = null;
        etapas[1]!["itensDeAvaliacao"] = JsonSerializer.SerializeToNode(new[] { new { rotulo = "Item", pontuacaoMaxima = 100m } });
        etapas[1]!["escalaDeAtendimento"] = JsonSerializer.SerializeToNode(new[] { new { rotulo = "Completo", fracao = 1m }, new { rotulo = "Parcial", fracao = 0.25m } });
        JsonNode leitura = await GravarELerEtapasAsync(cliente, processoId, etapas);
        leitura[1]!["avaliacao"]!["escalaDeAtendimento"]!.AsArray().Select(nivel => nivel!["fracao"]!.GetValue<decimal>()).Should().Equal(1m, 0.25m);
    }

    [Fact]
    public async Task Definir_PesoComCincoCasasENomeVazio_AcumulaRecusas()
    {
        (Guid processoId, Guid tipoBancaId) = await PrepararAsync();
        using HttpClient cliente = ambienteDeTeste.Factory.CreateClient();
        JsonArray etapas = EtapasComBanca(tipoBancaId);
        etapas[0]!["peso"] = 1.12345m;
        etapas[0]!["nome"] = "";
        using HttpResponseMessage resposta = await EnviarEtapasAsync(cliente, processoId, etapas);
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, corpo);
        corpo.Should().Contain("uniplus.validacao.formato").And.Contain("uniplus.selecao.etapa_processo.nome_obrigatorio");
    }

    [Fact]
    public async Task Definir_FracaoForaDoIntervalo_RecusaDoDominioCom422()
    {
        (Guid processoId, Guid tipoBancaId) = await PrepararAsync();
        using HttpClient cliente = ambienteDeTeste.Factory.CreateClient();
        JsonArray etapas = EtapasComBanca(tipoBancaId);
        etapas[0]!["escalaDeAtendimento"] = JsonSerializer.SerializeToNode(new[] { new { rotulo = "Completo", fracao = 1m }, new { rotulo = "Inválido", fracao = 1.5m } });
        using HttpResponseMessage resposta = await EnviarEtapasAsync(cliente, processoId, etapas);
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, corpo);
        corpo.Should().Contain("uniplus.validacao.formato").And.Contain("A fração do nível deve ter no máximo 4 casas decimais e ficar entre 0 e 1.");
    }

    [Fact]
    public async Task Definir_SemPermissao_RecusaSemDadosDoProcesso()
    {
        (Guid processoId, _) = await PrepararAsync();
        using HttpClient cliente = ambienteDeTeste.Factory.CreateClient();
        using HttpResponseMessage resposta = await EnviarAsync(cliente, processoId, new JsonArray(), Guid.CreateVersion7().ToString("N"), "candidato");
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await resposta.Content.ReadAsStringAsync()).Should().NotContain("etapas");
    }

    private async Task<(Guid ProcessoId, Guid TipoBancaId)> PrepararAsync()
    {
        (Guid processoId, _) = await PublicacaoPeloEndpointDeTeste.SemearProcessoAsync(ambienteDeTeste.Factory, nameof(GravacaoDasEtapasEndpointTests));
        await CatalogosDoEnvelopeFechado.SemearTipoDeAtoResultadoPreliminarAsync(ambienteDeTeste.Factory);
        CatalogosDoEnvelopeFechado.CatalogosSemeados catalogos = await CatalogosDoEnvelopeFechado.SemearCatalogosRicosAsync(ambienteDeTeste.Factory, Guid.CreateVersion7().ToString("N")[^8..]);
        return (processoId, catalogos.TipoBancaId);
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

    private static JsonArray EtapasComDecimaisDeQuatroCasas(Guid tipoBancaId)
    {
        JsonArray etapas = EtapasComBanca(tipoBancaId);
        etapas[0]!["peso"] = 1.1234m;
        etapas[0]!["notaMinima"] = 40.1234m;
        etapas[0]!["notaMaxima"] = 100.1234m;
        etapas[0]!["itensDeAvaliacao"] = JsonSerializer.SerializeToNode(new[] { new { rotulo = "Item", pontuacaoMaxima = 1.1234m } });
        etapas[1]!["notaMaxima"] = 0.0001m;
        etapas[1]!["passoDoLancamento"] = 0.0001m;
        return etapas;
    }

    private static JsonArray EtapasComRecurso(Guid tipoBancaId)
    {
        JsonArray etapas = EtapasComBanca(tipoBancaId);
        etapas[0]!["produtos"] = JsonSerializer.SerializeToNode(PayloadsDoEnvelopeFechado.ProdutosDoResultado());
        JsonObject regra = PayloadsDoEnvelopeFechado.RegraDeRecursoAncoradaNoResultado();
        regra["ancora"] = EventoInicialDoPrazo.PublicacaoDoAto.Codigo();
        etapas[0]!["recursos"] = new JsonArray(regra);
        return etapas;
    }

    private static async Task<JsonNode> GravarELerEtapasAsync(HttpClient cliente, Guid processoId, JsonArray etapas)
    {
        using HttpResponseMessage resposta = await EnviarEtapasAsync(cliente, processoId, etapas);
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
        return (await LerProcessoComoJsonAsync(cliente, processoId))["etapas"]!;
    }

    private static Task<HttpResponseMessage> EnviarEtapasAsync(HttpClient cliente, Guid processoId, JsonArray etapas) =>
        EnviarAsync(cliente, processoId, etapas, Guid.CreateVersion7().ToString("N"), "plataforma-admin");

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, Guid processoId, JsonArray etapas, string chave, string papel)
    {
        using HttpRequestMessage pedido = new(HttpMethod.Put, new Uri($"/api/selecao/processos-seletivos/{processoId}/etapas", UriKind.Relative))
        {
            Content = JsonContent.Create(etapas),
        };
        pedido.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        pedido.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, papel);
        pedido.Headers.TryAddWithoutValidation("Idempotency-Key", chave);
        return await cliente.SendAsync(pedido);
    }
}
