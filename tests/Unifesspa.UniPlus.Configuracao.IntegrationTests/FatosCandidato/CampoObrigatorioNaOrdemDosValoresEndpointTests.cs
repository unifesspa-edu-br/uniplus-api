namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FatosCandidato;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;

/// <summary>
/// A reordenação dos valores recusa, no envelope da ADR-0023, a lista de códigos omitida ou nula com 422 e o código
/// genérico de obrigatório; só a lista vazia declara "nenhum código", e a que deixa valor de fora segue recusada.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class CampoObrigatorioNaOrdemDosValoresEndpointTests(ConfiguracaoEndpointFixture fixture)
    : FatoCandidatoAdminEndpointTestBase(fixture)
{
    [Fact(DisplayName = "Reordenar sem a lista de códigos, ou com ela nula, é recusado com 422 no campo dos códigos")]
    public async Task ReordenarValores_ListaOmitidaOuNula_Retorna422()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await CadastrarAsync(client, FatoCategorico());
        await AcrescentarValorAsync(client, id, "EJA", "EJA", 0);

        await RecusaDeCampoObrigatorioNoDominio.ConferirAsync(await Reordenar(client, id, new { }), "codigos");
        await RecusaDeCampoObrigatorioNoDominio.ConferirAsync(await Reordenar(client, id, new { codigos = (string[]?)null }), "codigos");
    }

    [Fact(DisplayName = "Reordenar sem a lista de códigos em fato inexistente é recusado com 422, não com 404")]
    public async Task ReordenarValores_ListaOmitidaEFatoInexistente_Retorna422()
    {
        using HttpClient client = Fixture.Factory.CreateClient();

        await RecusaDeCampoObrigatorioNoDominio.ConferirAsync(await Reordenar(client, Guid.CreateVersion7(), new { }), "codigos");
    }

    [Fact(DisplayName = "Reordenar com lista vazia não é ausente: é recusado por faltar valor, não por obrigatório")]
    public async Task ReordenarValores_ListaVazia_RecusaPorValorAusenteNaOrdem()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await CadastrarAsync(client, FatoCategorico());
        await AcrescentarValorAsync(client, id, "EJA", "EJA", 0);

        HttpResponseMessage resposta = await Reordenar(client, id, new { codigos = Array.Empty<string>() });

        await RecusaDeCampoObrigatorioNoDominio.ConferirRecusasAsync(
            resposta, ("codigos", "uniplus.configuracao.fato_valor_dominio.ordem_sem_todos_os_valores"));
    }

    [Fact(DisplayName = "Reordenar com a lista completa continua aceito com 204")]
    public async Task ReordenarValores_ListaCompleta_Retorna204()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        Guid id = await CadastrarAsync(client, FatoCategorico());
        await AcrescentarValorAsync(client, id, "EJA", "EJA", 0);

        HttpResponseMessage resposta = await Reordenar(client, id, new { codigos = new[] { "EJA" } });

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static Task<HttpResponseMessage> Reordenar(HttpClient client, Guid id, object corpo) =>
        EnviarAsync(client, HttpMethod.Put, $"{Base}/{id}/ordem-dos-valores", corpo);
}
