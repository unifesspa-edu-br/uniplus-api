namespace Unifesspa.UniPlus.Host.IntegrationTests;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// <b>Todo header que o contrato promete ao cliente é legível de fato por uma aplicação web.</b>
/// </summary>
/// <remarks>
/// <para>
/// Uma resposta de origem cruzada só entrega ao JavaScript sete headers "seguros" — e nenhum dos
/// nossos é um deles. Declarar um header no contrato e esquecê-lo na lista exposta pelo CORS
/// produz o pior desfecho possível: o servidor o emite corretamente, o navegador o recebe, o
/// contrato o documenta, e <c>response.headers.get(...)</c> devolve nulo. Não há erro, não há
/// status diferente, não há nada a investigar — só um número que a tela nunca mostra.
/// </para>
/// <para>
/// A política CORS acompanha o contrato: o que cada endpoint declara é acrescentado à lista exposta
/// quando a aplicação sobe. Este teste prova a composição completa do host, e não só o mecanismo
/// isolado — um endpoint que emite um header e o declara tem de o ver exposto.
/// </para>
/// </remarks>
[Collection(MonolitoHostCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class HeaderDeclaradoEAlcancavelTests
{
    private readonly MonolitoPostgresFixture _fixture;

    public HeaderDeclaradoEAlcancavelTests(MonolitoPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Todo header declarado no contrato está exposto pelo CORS")]
    public void Contrato_QuandoEndpointDeclaraHeader_DeveExpoLoPeloCors()
    {
        IReadOnlyCollection<string> declarados = HeadersDoContrato.Declarados(_fixture.Factory.Services);

        declarados.Should().NotBeEmpty(
            "sem endpoint declarando header próprio, este teste não estaria verificando nada");

        IReadOnlyCollection<string> expostos = HeadersDoContrato.ExpostosPelaPoliticaPadrao(_fixture.Factory.Services);

        declarados.Should().BeSubsetOf(expostos,
            "um header emitido e documentado que o CORS não expõe é ilegível na SPA, sem erro que denuncie");
    }
}
