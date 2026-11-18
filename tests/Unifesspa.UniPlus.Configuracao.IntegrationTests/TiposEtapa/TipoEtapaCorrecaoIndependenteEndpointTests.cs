namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposEtapa;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>A declaração de que o tipo de etapa exige correção independente, como a API a recusa na escrita.</summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class TipoEtapaCorrecaoIndependenteEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public TipoEtapaCorrecaoIndependenteEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "POST sem o sinalizador de correção independente devolve 422 apontando o campo obrigatório")]
    public async Task Criar_SemExigeCorrecaoIndependente_Retorna422()
    {
        HttpResponseMessage response = await Criar(new
        {
            codigo = CodigoUnico(),
            nome = "Sem o sinalizador",
            conceitosAdmitidos = Array.Empty<string>(),
            admitePontuacao = true,
            admiteEliminacao = true,
        });

        await ConferirRecusa(response, "uniplus.configuracao.tipo_etapa.exige_correcao_independente_obrigatorio", "exigeCorrecaoIndependente");
    }

    [Fact(DisplayName = "POST exigindo correção independente sem admitir conceito devolve 422 no campo dos conceitos")]
    public async Task Criar_CorrecaoIndependenteSemConceito_Retorna422()
    {
        HttpResponseMessage response = await Criar(new
        {
            codigo = CodigoUnico(),
            nome = "Correção sem conceito",
            conceitosAdmitidos = Array.Empty<string>(),
            admitePontuacao = true,
            admiteEliminacao = true,
            exigeCorrecaoIndependente = true,
        });

        await ConferirRecusa(response, "uniplus.configuracao.tipo_etapa.correcao_independente_sem_conceito", "conceitosAdmitidos");
    }

    private static async Task ConferirRecusa(HttpResponseMessage response, string codigo, string campo)
    {
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString().Should().Be(codigo);
        problema.RootElement.GetProperty("errors")[0].GetProperty("field").GetString().Should().Be(campo);
    }

    private async Task<HttpResponseMessage> Criar(object body)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-etapa", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private static string CodigoUnico() => $"CI_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
}
