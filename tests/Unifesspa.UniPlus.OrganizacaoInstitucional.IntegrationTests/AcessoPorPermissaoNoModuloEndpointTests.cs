namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Permissoes;
using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

/// <summary>
/// Cada recurso do módulo decide o acesso pela permissão concedida, e não pelo papel do token: quem tem a concessão
/// passa, o administrador da plataforma sem concessão e quem só tem a permissão de outro recurso recebem 403, e sem
/// login é 401.
/// </summary>
[Collection(OrganizacaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class AcessoPorPermissaoNoModuloEndpointTests : IAsyncLifetime
{
    private const string Leandro = "leandro.viana";
    private const string Diogo = "diogo.souza";

    private readonly OrganizacaoEndpointFixture _fixture;

    public AcessoPorPermissaoNoModuloEndpointTests(OrganizacaoEndpointFixture fixture) => _fixture = fixture;

    public static TheoryData<PermissaoOrganizacao, string> Recursos => new()
    {
        { PermissaoOrganizacao.InstituicaoManter, "/api/organizacao/admin/instituicao" },
        { PermissaoOrganizacao.UnidadesManter, "/api/organizacao/admin/unidades" },
    };

    public async Task InitializeAsync()
    {
        foreach (string sub in new[] { Leandro, Diogo })
        {
            await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, sub);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory(DisplayName = "A concessão da permissão abre o recurso")]
    [MemberData(nameof(Recursos))]
    public async Task ComPermissao_Passa(PermissaoOrganizacao permissao, string rota)
    {
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(_fixture.Factory.Services, permissao.Codigo);
        await ConcessoesDeTeste.ConcederAsync(_fixture.Factory.Services, Leandro, perfil);

        using HttpResponseMessage resposta = await EnviarAsync(Leandro, rota);

        resposta.StatusCode.Should().NotBe(HttpStatusCode.Forbidden, permissao.Codigo).And.NotBe(HttpStatusCode.Unauthorized, permissao.Codigo);
        string codigo = permissao.Codigo;
        List<RegistroDecisaoAcesso> decisoes = [.. _fixture.Factory.Services.GetRequiredService<RegistroDeDecisoesDeTeste>().Registrados
            .Where(r => r.PerfilId == perfil && r.Permissao == codigo)];
        decisoes.Should().NotBeEmpty(codigo).And.OnlyContain(r => r.Permitido, codigo);
    }

    [Theory(DisplayName = "O administrador da plataforma sem concessão e a permissão de outro recurso respondem 403")]
    [MemberData(nameof(Recursos))]
    public async Task SemPermissao_403(PermissaoOrganizacao permissao, string rota)
    {
        PermissaoOrganizacao outra = permissao == PermissaoOrganizacao.InstituicaoManter ? PermissaoOrganizacao.UnidadesManter : PermissaoOrganizacao.InstituicaoManter;
        await ConcessoesDeTeste.ConcederPermissoesAsync(_fixture.Factory.Services, Leandro, outra.Codigo);

        using HttpResponseMessage administrador = await EnviarAsync(Diogo, rota, "plataforma-admin");
        using HttpResponseMessage outroRecurso = await EnviarAsync(Leandro, rota);

        administrador.StatusCode.Should().Be(HttpStatusCode.Forbidden, permissao.Codigo);
        outroRecurso.StatusCode.Should().Be(HttpStatusCode.Forbidden, permissao.Codigo);
    }

    [Theory(DisplayName = "Sem login a resposta é 401")]
    [MemberData(nameof(Recursos))]
    public async Task SemLogin_401(PermissaoOrganizacao permissao, string rota)
    {
        using HttpResponseMessage resposta = await EnviarAsync(null, rota);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized, permissao.Codigo);
    }

    private async Task<HttpResponseMessage> EnviarAsync(string? sub, string rota, string? papel = null)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(rota, UriKind.Relative))
        {
            Content = JsonContent.Create(new { }),
        };

        if (sub is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
            request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, sub);
            if (papel is not null)
            {
                request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, papel);
            }
        }

        return await client.SendAsync(request);
    }
}
