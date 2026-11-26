namespace Unifesspa.UniPlus.Host.IntegrationTests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

[Collection(MonolitoHostCollection.Name)]
public sealed class FiltroPorCodigoNaConsultaTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<HostAssemblyMarker> _host;

    public FiltroPorCodigoNaConsultaTests(MonolitoPostgresFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _host = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(servicos =>
            servicos.Replace(ServiceDescriptor.Singleton<TimeProvider>(new RelogioFixo()))));
    }

    public Task InitializeAsync() => ConcessoesDeTeste.RemoverDaPessoaAsync(_host.Services, CenarioDeFiltroPorCodigo.Pessoa);

    public async Task DisposeAsync()
    {
        await ConcessoesDeTeste.RemoverDaPessoaAsync(_host.Services, CenarioDeFiltroPorCodigo.Pessoa);
        await _host.DisposeAsync();
    }

    [Theory]
    [InlineData("/api/selecao/certames", "situacao", "uniplus.selecao.certame.filtro_de_situacao_invalido")]
    [InlineData("/api/selecao/processos-seletivos/{processo}/documentos-a-publicar", "finalidade", "uniplus.selecao.documento_a_publicar.filtro_de_finalidade_invalido")]
    [InlineData("/api/selecao/obrigatoriedades-legais", "categoria", "uniplus.selecao.obrigatoriedade_legal.filtro_de_categoria_invalido")]
    [InlineData("/api/selecao/regras-catalogo", "tipo", "uniplus.selecao.regra_catalogo.filtro_de_tipo_invalido")]
    [InlineData("/api/selecao/leiautes-de-arquivo-do-inep", "tipo", "uniplus.selecao.leiaute_do_inep.filtro_de_tipo_invalido")]
    [InlineData("/api/selecao/motivos-de-decisao", "tipo", "uniplus.selecao.motivo_de_decisao.filtro_de_tipo_invalido")]
    [InlineData("/api/selecao/motivos-de-decisao", "subclassificacao", "uniplus.selecao.motivo_de_decisao.filtro_de_subclassificacao_invalida")]
    [InlineData("/api/configuracao/admin/fatos-candidato", "origem", "uniplus.configuracao.fato_candidato.filtro_de_origem_invalida")]
    public async Task Listar_FiltroMarcado_RecusaEspacosEAceitaVazio(string rota, string parametro, string codigo)
    {
        ArgumentNullException.ThrowIfNull(rota);
        Guid processo = await CenarioDeFiltroPorCodigo.PrepararAsync(_host.Services);
        rota = rota.Replace("{processo}", processo.ToString(), StringComparison.Ordinal);
        using HttpClient cliente = Cliente();
        using HttpResponseMessage espacos = await LerAsync(cliente, $"{rota}?{parametro}=%20%20");
        espacos.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await espacos.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be(codigo);
        await ConferirEquivalenciaAsync(cliente, rota, $"{rota}?{parametro}=");
    }

    [Fact]
    public async Task Listar_TextoNaoMarcado_ComEspacosEquivaleAAusencia()
    {
        using HttpClient cliente = Cliente();
        await ConferirEquivalenciaAsync(cliente, "/api/organizacao/unidades", "/api/organizacao/unidades?q=%20%20");
    }

    private HttpClient Cliente()
    {
        HttpClient cliente = _host.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        cliente.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, CenarioDeFiltroPorCodigo.Pessoa);
        cliente.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, "Leandro Caleb Viana");
        cliente.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "leandro-viana84@uniplus-test.local");
        return cliente;
    }

    private static async Task ConferirEquivalenciaAsync(HttpClient cliente, string semFiltro, string comFiltro)
    {
        using HttpResponseMessage ausente = await LerAsync(cliente, semFiltro);
        using HttpResponseMessage informado = await LerAsync(cliente, comFiltro);
        ausente.StatusCode.Should().Be(HttpStatusCode.OK);
        informado.StatusCode.Should().Be(ausente.StatusCode);
        (await informado.Content.ReadAsStringAsync()).Should().Be(await ausente.Content.ReadAsStringAsync());
    }

    private static Task<HttpResponseMessage> LerAsync(HttpClient cliente, string rota) =>
        cliente.GetAsync(new Uri(rota, UriKind.Relative), CancellationToken.None);

    private sealed class RelogioFixo : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    }
}
