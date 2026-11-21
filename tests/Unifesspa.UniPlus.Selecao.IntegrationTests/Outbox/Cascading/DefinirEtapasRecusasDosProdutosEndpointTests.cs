namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDoCronogramaDeFasesPeloEndpoint;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.PublicacaoPeloEndpointDeTeste;

/// <summary>
/// Os produtos de uma etapa mal declarados chegando ao cliente, pelo HTTP: dois produtos recusados produzem
/// <b>duas</b> entradas em <c>errors[]</c>, cada uma com o campo do produto que a causou.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class DefinirEtapasRecusasDosProdutosEndpointTests(CascadingFixture fixture)
{
    private const string CodigoDoResultado = "RECUSAS_PRODUTO_ETAPA_RESULTADO";
    private const string CodigoForaDoCatalogo = "RECUSAS_PRODUTO_ETAPA_FORA_DO_CATALOGO";
    private const string PapelForaDoVocabulario = "PAPEL_INEXISTENTE";
    private static readonly Guid TipoProvaObjetiva = new("019fee1e-7000-7000-8000-000000000001");

    [Fact(DisplayName = "Dois produtos recusados na etapa devolvem 422 com as duas recusas em errors[], cada uma no seu produto")]
    public async Task DoisProdutosRecusados_422ComOsDoisErrosNoCampoDoProduto()
    {
        await TiposDeAtoSeeder.SemearTiposAsync(fixture.Factory.Services, [(CodigoDoResultado, "Resultado das recusas de produto da etapa", true)]);
        (Guid processoId, _) = await SemearProcessoAsync(fixture.Factory, nameof(DoisProdutosRecusados_422ComOsDoisErrosNoCampoDoProduto));
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await EnviarEtapaAsync(client, processoId);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await resposta.Content.ReadAsStringAsync());
        ErroDoProblema[] erros = await LerErrosDoProblemaAsync(resposta, "a recusa dos produtos sai em errors[], cada uma com o campo do produto");
        erros.Select(static e => (e.Campo, e.Codigo)).Should().Equal(
            ("etapas[0].produtos[0].papel", "uniplus.selecao.produto_da_etapa.papel_desconhecido"),
            ("etapas[0].produtos[2].atoCodigo", "uniplus.selecao.produto_da_etapa.ato_nao_encontrado_no_catalogo"));
    }

    private static async Task<HttpResponseMessage> EnviarEtapaAsync(HttpClient client, Guid processoId)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Put, new Uri($"/api/selecao/processos-seletivos/{processoId}/etapas", UriKind.Relative))
        {
            Content = JsonContent.Create(new[] { EtapaComDoisProdutosRecusados() }),
        };
        Autenticar(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request);
    }

    private static object EtapaComDoisProdutosRecusados() => new
    {
        id = (Guid?)null,
        nome = "Prova Objetiva",
        carater = (int)CaraterEtapa.Classificatoria,
        tipoEtapaOrigemId = TipoProvaObjetiva,
        peso = 1m,
        notaMinima = (decimal?)null,
        notaMaxima = 100m,
        formaDeAvaliacao = "OBJETIVA",
        ordem = 1,
        faseCodigo = "RESULTADO_FINAL",
        produtos = ProdutosComPapelDesconhecidoEAtoForaDoCatalogo(),
        bancas = Array.Empty<object>(),
        recursos = Array.Empty<object>(),
    };

    private static object[] ProdutosComPapelDesconhecidoEAtoForaDoCatalogo() =>
    [
        new { atoCodigo = CodigoDoResultado, papel = (string?)PapelForaDoVocabulario },
        new { atoCodigo = CodigoDoResultado, papel = (string?)PapelProdutoFaseCodigo.Preliminar },
        new { atoCodigo = CodigoForaDoCatalogo, papel = (string?)null },
    ];
}
