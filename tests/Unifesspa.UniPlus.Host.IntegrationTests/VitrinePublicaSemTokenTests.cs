namespace Unifesspa.UniPlus.Host.IntegrationTests;

using System.Diagnostics.CodeAnalysis;
using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Com a autenticação exigida por padrão, a vitrine pública de processos seletivos, que declara
/// <c>[AllowAnonymous]</c>, continua respondendo a quem não tem token.
/// </summary>
[Collection(MonolitoHostCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class VitrinePublicaSemTokenTests
{
    private readonly MonolitoPostgresFixture _fixture;

    public VitrinePublicaSemTokenTests(MonolitoPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Vitrine pública de processos seletivos responde sem token")]
    public async Task Vitrine_RespondeSemToken()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await client.GetAsync(new Uri("/api/selecao/certames", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
