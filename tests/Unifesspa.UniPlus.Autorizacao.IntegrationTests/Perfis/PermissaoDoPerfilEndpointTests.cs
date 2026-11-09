namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;

/// <summary>Toda operação sobre perfis exige a permissão de manter perfis; sem autenticação a resposta é 401.</summary>
[Collection(AutorizacaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class PermissaoDoPerfilEndpointTests : PerfilEndpointTestBase
{
    public PermissaoDoPerfilEndpointTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Theory(DisplayName = "Sem a permissão de manter perfis, toda operação é 403, inclusive a leitura")]
    [InlineData("GET", "")]
    [InlineData("POST", "")]
    [InlineData("DELETE", "/019a0000-0000-7000-8000-000000000001/ativacao")]
    [InlineData("POST", "/019a0000-0000-7000-8000-000000000001/versoes")]
    [InlineData("DELETE", "/019a0000-0000-7000-8000-000000000001/versoes/019a0000-0000-7000-8000-0000000000aa")]
    [InlineData("GET", "/019a0000-0000-7000-8000-000000000001/versoes/019a0000-0000-7000-8000-0000000000aa/documento")]
    public async Task SemPermissao_Devolve403(string metodo, string sufixo)
    {
        using HttpClient isabella = await ClienteComPermissoesAsync("leandro.viana", ConsultarCatalogo);

        using HttpResponseMessage resposta = await EnviarAsync(
            isabella, new HttpMethod(metodo), Rota + sufixo, metodo == "POST" ? Corpo(Nome(), [ConsultarCatalogo]) : null);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "Sem autenticação a leitura é 401, não 403")]
    public async Task SemAutenticacao_Devolve401()
    {
        using HttpClient anonimo = Fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await anonimo.GetAsync(new Uri(Rota, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
