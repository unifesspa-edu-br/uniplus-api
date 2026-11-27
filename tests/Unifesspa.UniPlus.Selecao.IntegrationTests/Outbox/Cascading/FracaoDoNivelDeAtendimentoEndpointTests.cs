namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDoCronogramaDeFasesPeloEndpoint;

/// <summary>
/// A fração de cada nível da escala de atendimento é declarada pelo pedido: a falta recusa com 422 no campo, sem gravar nada;
/// zero e um declarados valem; fora da faixa e com casas demais têm cada um o seu código.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class FracaoDoNivelDeAtendimentoEndpointTests(CascadingFixture ambiente)
{
    private const string CampoDaFracao = "etapas[1].escalaDeAtendimento[0].fracao";

    // A escala exige um nível de fração 1; o segundo nível a cumpre, para a fração do primeiro ser a única recusa.
    private static readonly object NivelCompleto = new { rotulo = "Completo", fracao = 1m };

    [Fact(DisplayName = "A fração omitida é recusada no campo, sem gravar nada")]
    public async Task FracaoOmitida_Recusa422NoCampo()
    {
        (HttpClient cliente, Guid processoId, JsonArray etapas) = await PrepararAsync();
        etapas[1]!["escalaDeAtendimento"] = JsonSerializer.SerializeToNode(new object[] { new { rotulo = "Parcial" }, NivelCompleto });

        await ExigirRecusaSemGravarAsync(cliente, processoId, etapas, "uniplus.validacao.obrigatorio");
    }

    [Fact(DisplayName = "A fração nula é recusada como a omitida")]
    public async Task FracaoNula_Recusa422NoCampo()
    {
        (HttpClient cliente, Guid processoId, JsonArray etapas) = await PrepararAsync();
        etapas[1]!["escalaDeAtendimento"] = JsonSerializer.SerializeToNode(new object[] { new { rotulo = "Parcial", fracao = (decimal?)null }, NivelCompleto });

        await ExigirRecusaSemGravarAsync(cliente, processoId, etapas, "uniplus.validacao.obrigatorio");
    }

    [Fact(DisplayName = "As frações zero e um declaradas são aceitas e lidas de volta")]
    public async Task FracoesNosLimites_AceitasELidasDeVolta()
    {
        (HttpClient cliente, Guid processoId, JsonArray etapas) = await PrepararAsync();
        etapas[1]!["escalaDeAtendimento"] = JsonSerializer.SerializeToNode(new[] { new { rotulo = "Nenhum", fracao = 0m }, new { rotulo = "Completo", fracao = 1m } });

        using HttpResponseMessage resposta = await EnviarAsync(cliente, processoId, etapas);

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
        JsonNode lida = (await LerProcessoComoJsonAsync(cliente, processoId))["etapas"]!;
        lida[1]!["avaliacao"]!["escalaDeAtendimento"]!.AsArray().Select(static nivel => nivel!["fracao"]!.GetValue<decimal>()).Should().Equal(0m, 1m);
    }

    [Theory(DisplayName = "A fração fora da faixa e a com casas demais são recusadas, cada uma com o seu código")]
    [InlineData(1.5, "uniplus.selecao.nivel_de_atendimento.fracao_invalida")]
    [InlineData(-0.1, "uniplus.selecao.nivel_de_atendimento.fracao_invalida")]
    [InlineData(0.12345, "uniplus.validacao.formato")]
    public async Task FracaoInvalida_RecusaComOCodigoDaCausa(double fracao, string codigo)
    {
        (HttpClient cliente, Guid processoId, JsonArray etapas) = await PrepararAsync();
        etapas[1]!["escalaDeAtendimento"] = JsonSerializer.SerializeToNode(new object[] { new { rotulo = "Parcial", fracao = (decimal)fracao }, NivelCompleto });

        await ExigirRecusaSemGravarAsync(cliente, processoId, etapas, codigo);
    }

    private static async Task ExigirRecusaSemGravarAsync(HttpClient cliente, Guid processoId, JsonArray etapas, string codigo)
    {
        string etapasAntes = (await LerProcessoComoJsonAsync(cliente, processoId))["etapas"]!.ToJsonString();

        using HttpResponseMessage resposta = await EnviarAsync(cliente, processoId, etapas);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await resposta.Content.ReadAsStringAsync());
        ErroDoProblema[] erros = await LerErrosDoProblemaAsync(resposta);
        erros.Should().ContainSingle().Which.Should().Be(new ErroDoProblema(CampoDaFracao, codigo));
        (await LerProcessoComoJsonAsync(cliente, processoId))["etapas"]!.ToJsonString().Should().Be(etapasAntes);
    }

    private async Task<(HttpClient Cliente, Guid ProcessoId, JsonArray Etapas)> PrepararAsync()
    {
        (Guid processoId, _) = await PublicacaoPeloEndpointDeTeste.SemearProcessoAsync(ambiente.Factory, nameof(FracaoDoNivelDeAtendimentoEndpointTests));
        await CatalogosDoEnvelopeFechado.SemearTipoDeAtoResultadoPreliminarAsync(ambiente.Factory);
        CatalogosDoEnvelopeFechado.CatalogosSemeados catalogos = await CatalogosDoEnvelopeFechado.SemearCatalogosRicosAsync(ambiente.Factory, Guid.CreateVersion7().ToString("N")[^8..]);
        JsonArray etapas = JsonSerializer.SerializeToNode(PayloadsDoEnvelopeFechado.EtapasIniciais(catalogos.TipoBancaId))!.AsArray();
        foreach (JsonNode? etapa in etapas)
        {
            etapa!["faseCodigo"] = "RESULTADO_FINAL";
        }
        etapas[1]!["passoDoLancamento"] = null;
        etapas[1]!["itensDeAvaliacao"] = JsonSerializer.SerializeToNode(new[] { new { rotulo = "Item", pontuacaoMaxima = 100m } });
        return (ambiente.Factory.CreateClient(), processoId, etapas);
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, Guid processoId, JsonArray etapas)
    {
        using HttpRequestMessage pedido = new(HttpMethod.Put, new Uri($"/api/selecao/processos-seletivos/{processoId}/etapas", UriKind.Relative))
        {
            Content = JsonContent.Create(etapas),
        };
        Autenticar(pedido);
        pedido.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await cliente.SendAsync(pedido);
    }
}
