namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FatosCandidato;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;

/// <summary>
/// As regras padrão de um derivado recusam, no envelope da ADR-0023, o fato que a condição omite ou traz com
/// <c>null</c>, com o ponteiro do campo através das duas listas aninhadas. As duas formas de ausência dão a
/// mesma resposta, e uma condição completa continua sendo gravada.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class CampoObrigatorioNasRegrasPadraoEndpointTests(ConfiguracaoEndpointFixture fixture)
    : FatoCandidatoAdminEndpointTestBase(fixture)
{
    private const string CampoDoFato = "regras[0].quando[0][0].fato";

    [Fact(DisplayName = "Fato omitido na condição da regra padrão é recusado com 400 no envelope e o ponteiro com os índices")]
    public async Task DefinirRegrasPadrao_FatoOmitido_Retorna400NoEnvelope()
    {
        using HttpClient client = Fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await PutRegrasAsync(client, new { operador = "IGUAL", valor = true });

        await RecusaDeCampoObrigatorioAusente.ConferirAsync(resposta, CampoDoFato);
    }

    [Fact(DisplayName = "Fato null na condição da regra padrão dá a mesma resposta do omitido")]
    public async Task DefinirRegrasPadrao_FatoNull_DaAMesmaRespostaDoOmitido()
    {
        using HttpClient client = Fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await PutRegrasAsync(client, new { fato = (string?)null, operador = "IGUAL", valor = true });

        await RecusaDeCampoObrigatorioAusente.ConferirAsync(resposta, CampoDoFato);
    }

    [Fact(DisplayName = "Condição completa continua sendo gravada com 204")]
    public async Task DefinirRegrasPadrao_CondicaoCompleta_Retorna204()
    {
        using HttpClient client = Fixture.Factory.CreateClient();
        string dependencia = CodigoUnico();
        await CadastrarAsync(client, FatoBooleano(dependencia));

        HttpResponseMessage resposta = await PutRegrasAsync(client, new { fato = dependencia, operador = "IGUAL", valor = true });

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
    }

    private static async Task<HttpResponseMessage> PutRegrasAsync(HttpClient client, object condicao)
    {
        Guid id = await CadastrarAsync(client, DerivadoCategorico(), "/derivados");
        await AcrescentarValorAsync(client, id, "EJA", "Educação de jovens e adultos", 0);
        object corpo = new { regras = new[] { new { contribui = "EJA", quando = new[] { new[] { condicao } } } } };

        return await EnviarAsync(client, HttpMethod.Put, $"{Base}/{id}/regras-padrao", corpo);
    }
}
