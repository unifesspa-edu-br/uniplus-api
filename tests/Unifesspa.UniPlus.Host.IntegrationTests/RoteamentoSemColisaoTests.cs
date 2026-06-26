namespace Unifesspa.UniPlus.Host.IntegrationTests;

using System.Diagnostics.CodeAnalysis;
using System.Net;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Com os 4 módulos co-hospedados num processo único, o
/// roteamento HTTP é compartilhado. Estes testes confirmam que (a) as rotas dos
/// módulos são namespaced por prefixo <c>api/{modulo}/</c> e (b) não há colisão
/// — nenhum par (método HTTP, template) é atendido por mais de um endpoint, o
/// que em runtime causaria <c>AmbiguousMatchException</c>.
/// </summary>
/// <remarks>
/// O prefixo por módulo (Configuracao→<c>api/configuracao</c>,
/// Organizacao→<c>api/organizacao</c>, Selecao→<c>api/selecao</c>) é o mecanismo
/// estrutural que torna a colisão impossível por construção: dois módulos podem
/// expor o mesmo recurso (ex.: <c>admin/...</c>) sem conflito, pois os caminhos
/// completos divergem no segmento do módulo.
/// </remarks>
[Collection(MonolitoHostCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class RoteamentoSemColisaoTests
{
    private readonly MonolitoPostgresFixture _fixture;

    public RoteamentoSemColisaoTests(MonolitoPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Nenhum par (método, template) é atendido por 2+ endpoints no monólito")]
    public void RotasDoMonolito_NaoColidem()
    {
        EndpointDataSource dataSource =
            _fixture.Factory.Services.GetRequiredService<EndpointDataSource>();

        var pares = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(endpoint =>
            {
                IReadOnlyList<string> metodos =
                    endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods
                    ?? ["*"];
                string template = endpoint.RoutePattern.RawText ?? string.Empty;
                return metodos.Select(metodo => (Metodo: metodo, Template: template));
            })
            .ToList();

        IReadOnlyList<string> colisoes = pares
            .GroupBy(p => p)
            .Where(grupo => grupo.Count() > 1)
            .Select(grupo => $"{grupo.Key.Metodo} {grupo.Key.Template} (x{grupo.Count()})")
            .ToList();

        colisoes.Should().BeEmpty(
            "no monólito modular os 4 módulos compartilham o pipeline de roteamento; "
            + "o prefixo api/<modulo>/ deve garantir templates únicos por (método, caminho)");
    }

    [Theory(DisplayName = "Cada módulo expõe rotas sob seu prefixo api/{modulo}/")]
    [InlineData("api/configuracao/")]
    [InlineData("api/organizacao/")]
    [InlineData("api/selecao/")]
    public void RotasDeModulo_SaoNamespacedPorPrefixo(string prefixo)
    {
        EndpointDataSource dataSource =
            _fixture.Factory.Services.GetRequiredService<EndpointDataSource>();

        IEnumerable<string> templates = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText ?? string.Empty);

        templates.Should().Contain(
            t => t.StartsWith(prefixo, StringComparison.Ordinal),
            $"o módulo deve expor seus controllers sob {prefixo} no monólito co-hospedado");
    }

    [Fact(DisplayName = "Smoke: pipeline HTTP vivo (/health/live 200) e rotas de módulo resolvem (200)")]
    public async Task SmokeHttp_PipelineVivoERotasDeModuloResolvem()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage live = await client.GetAsync(new Uri("/health/live", UriKind.Relative));
        live.StatusCode.Should().Be(HttpStatusCode.OK, "o liveness do monólito deve responder");

        // GET de lista é [AllowAnonymous] e retorna 200 (lista, possivelmente
        // populada por outros testes da coleção) — prova que a rota namespaced
        // resolve no processo co-hospedado, não 404.
        HttpResponseMessage campi = await client.GetAsync(
            new Uri("/api/configuracao/campi", UriKind.Relative));
        campi.StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage unidades = await client.GetAsync(
            new Uri("/api/organizacao/unidades", UriKind.Relative));
        unidades.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
