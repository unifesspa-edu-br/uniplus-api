namespace Unifesspa.UniPlus.Host.IntegrationTests;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using AwesomeAssertions;

using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Cors;
using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using FrameworkCorsOptions = Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions;

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
/// A declaração no contrato e a exposição no CORS vivem em arquivos distintos, e nada além deste
/// teste obriga as duas a andarem juntas. É o item de checklist que o header novo não pode pular.
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
        IReadOnlyCollection<string> declarados = HeadersDeclaradosPelosEndpoints();

        declarados.Should().NotBeEmpty(
            "sem endpoint declarando header próprio, este teste não estaria verificando nada");

        IReadOnlyCollection<string> expostos = HeadersExpostosPeloCors();

        declarados.Should().BeSubsetOf(expostos,
            "um header emitido e documentado que o CORS não expõe é ilegível na SPA, sem erro que denuncie");
    }

    /// <summary>
    /// Nomes que os endpoints prometem no contrato: os headers próprios de cada rota e o selo de
    /// entidade, que é declarado pelo seu próprio atributo.
    /// </summary>
    private HashSet<string> HeadersDeclaradosPelosEndpoints()
    {
        EndpointDataSource dataSource = _fixture.Factory.Services.GetRequiredService<EndpointDataSource>();

        IEnumerable<MethodInfo> acoes = dataSource.Endpoints
            .Select(static endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>())
            .Where(static descritor => descritor is not null)
            .Select(static descritor => descritor!.MethodInfo)
            .Distinct();

        HashSet<string> nomes = new(StringComparer.OrdinalIgnoreCase);
        foreach (MethodInfo acao in acoes)
        {
            foreach (EmiteHeaderAttribute header in acao.GetCustomAttributes<EmiteHeaderAttribute>(inherit: true))
            {
                nomes.Add(header.Nome);
            }

            if (acao.GetCustomAttribute<EmiteETagAttribute>(inherit: true) is not null)
            {
                nomes.Add("ETag");
            }
        }

        return nomes;
    }

    private IReadOnlyCollection<string> HeadersExpostosPeloCors()
    {
        FrameworkCorsOptions opcoes = _fixture.Factory.Services.GetRequiredService<IOptions<FrameworkCorsOptions>>().Value;
        CorsPolicy? politica = opcoes.GetPolicy(CorsConfiguration.DefaultPolicyName);

        politica.Should().NotBeNull("a política padrão é a que o pipeline aplica");

        return [.. politica!.ExposedHeaders];
    }
}
