namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Vocabularios;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Contrato HTTP dos vocabulários do resultado de uma regra (<c>Ternario</c>) e do estado de um fato
/// (<c>EstadoFato</c>): rota, acesso sem autenticação, tipo de mídia, ausência de cache e ordem de declaração.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class VocabulariosDaAvaliacaoDeFormularioEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public VocabulariosDaAvaliacaoDeFormularioEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /vocabularios/ternarios devolve código e rótulo na ordem do enum, sem exigir autenticação nem cabeçalho de cache")]
    public async Task ListarTernarios_DevolveCodigoERotuloNaOrdemDoEnum()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/configuracao/vocabularios/ternarios", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/vnd.uniplus.codigo-ternario.v1+json");
        response.Headers.CacheControl.Should().BeNull();
        CodigoComRotulo<Ternario>[] ternarios = (await response.Content.ReadFromJsonAsync<CodigoComRotulo<Ternario>[]>())!;
        ternarios.Select(static t => (t.Codigo, t.Rotulo)).Should().Equal(
            ("INDETERMINADO", "Indeterminado"),
            ("VERDADEIRO", "Verdadeiro"),
            ("FALSO", "Falso"));
    }

    [Fact(DisplayName = "GET /vocabularios/estados-de-fato devolve código e rótulo na ordem do enum, sem exigir autenticação nem cabeçalho de cache")]
    public async Task ListarEstadosFato_DevolveCodigoERotuloNaOrdemDoEnum()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/configuracao/vocabularios/estados-de-fato", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/vnd.uniplus.codigo-estado-de-fato.v1+json");
        response.Headers.CacheControl.Should().BeNull();
        CodigoComRotulo<EstadoFato>[] estados = (await response.Content.ReadFromJsonAsync<CodigoComRotulo<EstadoFato>[]>())!;
        estados.Select(static e => (e.Codigo, e.Rotulo)).Should().Equal(
            ("INDETERMINADO", "Indeterminado"),
            ("NAO_APLICAVEL", "Não se aplica"),
            ("RESOLVIDO", "Resolvido"),
            ("NAO_INFORMADO", "Não informado"));
    }
}
