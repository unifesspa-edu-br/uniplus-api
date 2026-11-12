namespace Unifesspa.UniPlus.Host.IntegrationTests.Governanca;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// O catálogo de permissões é lido por quem exerce só um dos três perfis de governança, pelo host real: gestor de
/// acessos, Representante do controlador e Encarregado de dados. Cecília Moura recebe cada perfil sozinha.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class CatalogoNosPerfisDeGovernancaNoHostTests : PedidosDeConcessaoTestBase
{
    private static readonly Persona Cecilia = new("cecilia.moura", "Cecília Ester Aurora Moura", "cecilia_ester_moura@uniplus-test.local");

    public CatalogoNosPerfisDeGovernancaNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Theory(DisplayName = "Cada perfil de governança, sozinho, lê o catálogo de permissões")]
    [InlineData(TipoDeGovernanca.GestorDeAcessos)]
    [InlineData(TipoDeGovernanca.RepresentanteDoControlador)]
    [InlineData(TipoDeGovernanca.EncarregadoDeDados)]
    public async Task PerfilDeGovernancaIsolado_LeOCatalogo(TipoDeGovernanca governanca)
    {
        await ConcessoesDeTeste.RemoverDaPessoaAsync(Fixture.Factory.Services, Cecilia.Sub.ToString());
        await ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, Cecilia.Sub.ToString(), governanca, Cecilia.Email);

        using HttpClient cecilia = Cliente(Cecilia);
        using HttpResponseMessage resposta = await cecilia.GetAsync(new Uri("/api/autorizacao/permissoes", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
    }
}
