namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Vocabularios;

using System.Net;
using System.Net.Http.Headers;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// Contrato HTTP dos vocabulários da modalidade de concorrência: a natureza legal, a composição de vagas, a regra de remanejamento e a ação quando indeferido, com código e rótulo
/// literais, na ordem de declaração, sem exigir autenticação.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class VocabulariosDaModalidadeEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public VocabulariosDaModalidadeEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed record ItemDoVocabulario(string Codigo, string Rotulo);

    [Fact(DisplayName = "GET /vocabularios/naturezas-legais devolve as três naturezas, na ordem de declaração, sem exigir autenticação")]
    public async Task ListarNaturezasLegais_DevolveAsTresNaturezas()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        ItemDoVocabulario[] itens = await ApoioDosVocabularios.LerAsync<ItemDoVocabulario>(
            client, "naturezas-legais", "codigo-natureza-legal");

        itens.Should().Equal(
            new ItemDoVocabulario("COTA_RESERVADA", "Cota reservada"),
            new ItemDoVocabulario("AMPLA", "Ampla concorrência"),
            new ItemDoVocabulario("ACAO_AFIRMATIVA", "Ação afirmativa"));
    }

    [Fact(DisplayName = "GET /vocabularios/acoes-quando-indeferido devolve as duas ações, na ordem de declaração, sem exigir autenticação")]
    public async Task ListarAcoesQuandoIndeferido_DevolveAsDuasAcoes()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        ItemDoVocabulario[] itens = await ApoioDosVocabularios.LerAsync<ItemDoVocabulario>(
            client, "acoes-quando-indeferido", "codigo-acao-quando-indeferido");

        itens.Should().Equal(
            new ItemDoVocabulario("RECLASSIFICAR_AC", "Reclassificar na ampla concorrência"),
            new ItemDoVocabulario("RECLASSIFICAR_REGRA_EDITAL", "Reclassificar conforme a regra do edital"));
    }

    [Fact(DisplayName = "GET /vocabularios/composicoes-vagas devolve as quatro composições, na ordem de declaração, sem exigir autenticação")]
    public async Task ListarComposicoesVagas_DevolveAsQuatroComposicoes()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        ItemDoVocabulario[] itens = await ApoioDosVocabularios.LerAsync<ItemDoVocabulario>(
            client, "composicoes-vagas", "codigo-composicao-vagas");

        itens.Should().Equal(
            new ItemDoVocabulario("RESTANTE_DAS_VAGAS_OFERTADAS", "Restante das vagas ofertadas"),
            new ItemDoVocabulario("DENTRO_DA_RESERVA_LEGAL", "Dentro da reserva legal"),
            new ItemDoVocabulario("RETIRADA_DE_OUTRA_MODALIDADE", "Retirada de outra modalidade"),
            new ItemDoVocabulario("SUPLEMENTAR_AO_TOTAL", "Suplementar ao total"));
    }

    [Fact(DisplayName = "GET /vocabularios/regras-remanejamento devolve as três regras, na ordem de declaração, sem exigir autenticação")]
    public async Task ListarRegrasRemanejamento_DevolveAsTresRegras()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        ItemDoVocabulario[] itens = await ApoioDosVocabularios.LerAsync<ItemDoVocabulario>(
            client, "regras-remanejamento", "codigo-regra-remanejamento");

        itens.Should().Equal(
            new ItemDoVocabulario("SEGUE_CASCATA", "Segue a cascata"),
            new ItemDoVocabulario("DESTINO_UNICO", "Destino único"),
            new ItemDoVocabulario("CRUZADO", "Cruzado"));
    }

    [Theory(DisplayName = "Versão inexistente da vendor MIME do vocabulário da modalidade recusa com 406")]
    [InlineData("/api/configuracao/vocabularios/naturezas-legais", "codigo-natureza-legal")]
    [InlineData("/api/configuracao/vocabularios/acoes-quando-indeferido", "codigo-acao-quando-indeferido")]
    [InlineData("/api/configuracao/vocabularios/composicoes-vagas", "codigo-composicao-vagas")]
    [InlineData("/api/configuracao/vocabularios/regras-remanejamento", "codigo-regra-remanejamento")]
    public async Task Vocabularios_VersaoDesconhecidaDaVendorMime_Recusa(string rota, string recurso)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(rota, UriKind.Relative));
        request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse($"application/vnd.uniplus.{recurso}.v9+json"));

        using HttpResponseMessage resposta = await client.SendAsync(request);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
    }
}
