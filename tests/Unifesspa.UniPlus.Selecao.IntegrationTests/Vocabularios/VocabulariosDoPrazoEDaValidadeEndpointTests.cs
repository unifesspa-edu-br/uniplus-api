namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Vocabularios;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AwesomeAssertions;

using Outbox.Cascading;

[Collection(CascadingCollection.Name)]
public sealed class VocabulariosDoPrazoEDaValidadeEndpointTests(CascadingFixture fixture)
{
    [Theory]
    [InlineData("escolhas-prazo-emissao", "escolha-prazo-emissao", new[] { "SEM_PRAZO", "COM_PRAZO" })]
    [InlineData("escolhas-validade", "escolha-validade", new[] { "NAO_EXIGE_DOCUMENTO_VALIDO", "EXIGE_DOCUMENTO_VALIDO" })]
    [InlineData("unidades-prazo-emissao", "unidade-prazo-emissao", new[] { "DIAS", "MESES", "ANOS" })]
    [InlineData("tipos-data-referencia", "tipo-data-referencia", new[] { "FIM_DAS_INSCRICOES", "INICIO_DA_FASE", "FIM_DA_FASE", "DATA_ESPECIFICA", "DATA_DA_APRESENTACAO" })]
    [InlineData("efeitos-analise-documento", "efeito-analise-documento", new[] { "RECUSAR_DOCUMENTO", "ACEITAR_COM_AVISO" })]
    public async Task Listar_SemAutenticacao_DevolveCodigosERotulos(string rota, string recurso, string[] codigos)
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync(new Uri($"/api/selecao/vocabularios/{rota}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be($"application/vnd.uniplus.{recurso}.v1+json");
        Opcao[]? opcoes = await response.Content.ReadFromJsonAsync<Opcao[]>();
        opcoes.Should().NotBeNull();
        opcoes!.Select(static opcao => opcao.Codigo).Should().Equal(codigos);
        opcoes.Should().AllSatisfy(static opcao => opcao.Rotulo.Should().NotBeNullOrWhiteSpace());
    }

    [Theory]
    [InlineData("escolhas-prazo-emissao", "escolha-prazo-emissao")]
    [InlineData("escolhas-validade", "escolha-validade")]
    [InlineData("unidades-prazo-emissao", "unidade-prazo-emissao")]
    [InlineData("tipos-data-referencia", "tipo-data-referencia")]
    [InlineData("efeitos-analise-documento", "efeito-analise-documento")]
    public async Task Listar_VersaoDesconhecida_Recusa(string rota, string recurso)
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri($"/api/selecao/vocabularios/{rota}", UriKind.Relative));
        request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse($"application/vnd.uniplus.{recurso}.v9+json"));

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
    }

    private sealed record Opcao(string Codigo, string Rotulo);
}
