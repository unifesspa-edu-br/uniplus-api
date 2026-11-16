namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class DivergenciasDaGovernancaEndpointTests : PerfilEndpointTestBase, IAsyncLifetime
{
    private readonly CenarioDaReclassificacao _cenario;

    public DivergenciasDaGovernancaEndpointTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
        _cenario = new CenarioDaReclassificacao(Fixture);
    }

    public Task InitializeAsync() => _cenario.ConciliarAsync();

    public Task DisposeAsync() => _cenario.ConciliarAsync();

    [Fact(DisplayName = "A divergência da governança aparece na tela de quem a vê e é negada a quem não tem permissão")]
    public async Task Divergencias_SoParaQuemTemPermissao()
    {
        await ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, Diogo, TipoDeGovernanca.RepresentanteDoControlador);
        using HttpClient representante = Cliente(Diogo);
        using HttpClient semPermissao = Cliente($"pessoa.{Guid.NewGuid():N}");

        using HttpResponseMessage permitida = await representante.GetAsync(new Uri("/api/autorizacao/governanca/divergencias", UriKind.Relative));
        using HttpResponseMessage negada = await semPermissao.GetAsync(new Uri("/api/autorizacao/governanca/divergencias", UriKind.Relative));

        permitida.StatusCode.Should().Be(HttpStatusCode.OK);
        negada.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
