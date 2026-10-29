namespace Unifesspa.UniPlus.Configuracao.IntegrationTests;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Cada cadastro do módulo decide o acesso pela permissão concedida, e não pelo papel do token: quem tem a concessão
/// passa, o administrador da plataforma sem concessão e quem só tem a permissão de outro cadastro recebem 403, e sem
/// login é 401. A rota administrativa de cada recurso sai do código da permissão (<c>configuracao:{recurso}:{ação}</c>):
/// a manutenção é exercida por um POST e a consulta por um GET.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class AcessoPorPermissaoNoModuloEndpointTests : IAsyncLifetime
{
    private const string Leandro = "leandro.viana";
    private const string Diogo = "diogo.souza";

    private readonly ConfiguracaoEndpointFixture _fixture;

    public AcessoPorPermissaoNoModuloEndpointTests(ConfiguracaoEndpointFixture fixture) => _fixture = fixture;

    public static TheoryData<PermissaoConfiguracao> Permissoes
    {
        get
        {
            TheoryData<PermissaoConfiguracao> dados = [];
            foreach (PermissaoConfiguracao permissao in Enum.GetValues<PermissaoConfiguracao>())
            {
                dados.Add(permissao);
            }

            return dados;
        }
    }

    public async Task InitializeAsync()
    {
        foreach (string sub in new[] { Leandro, Diogo })
        {
            await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, sub);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory(DisplayName = "A concessão da permissão abre o cadastro")]
    [MemberData(nameof(Permissoes))]
    public async Task ComPermissao_Passa(PermissaoConfiguracao permissao)
    {
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(_fixture.Factory.Services, permissao.Codigo);
        await ConcessoesDeTeste.ConcederAsync(_fixture.Factory.Services, Leandro, perfil);

        using HttpResponseMessage resposta = await EnviarAsync(Leandro, permissao);

        resposta.StatusCode.Should().NotBe(HttpStatusCode.Forbidden, permissao.Codigo).And.NotBe(HttpStatusCode.Unauthorized, permissao.Codigo);
        string codigo = permissao.Codigo;
        List<RegistroDecisaoAcesso> decisoes = [.. _fixture.Factory.Services.GetRequiredService<RegistroDeDecisoesDeTeste>().Registrados
            .Where(r => r.PerfilId == perfil && r.Permissao == codigo)];
        decisoes.Should().NotBeEmpty(codigo).And.OnlyContain(r => r.Permitido, codigo);
    }

    [Theory(DisplayName = "O administrador da plataforma sem concessão e a permissão de outro cadastro respondem 403")]
    [MemberData(nameof(Permissoes))]
    public async Task SemPermissao_403(PermissaoConfiguracao permissao)
    {
        PermissaoConfiguracao outra = permissao == PermissaoConfiguracao.CampiManter
            ? PermissaoConfiguracao.CursosManter
            : PermissaoConfiguracao.CampiManter;
        await ConcessoesDeTeste.ConcederPermissoesAsync(_fixture.Factory.Services, Leandro, outra.Codigo);

        using HttpResponseMessage administrador = await EnviarAsync(Diogo, permissao, "plataforma-admin");
        using HttpResponseMessage outroCadastro = await EnviarAsync(Leandro, permissao);

        administrador.StatusCode.Should().Be(HttpStatusCode.Forbidden, permissao.Codigo);
        outroCadastro.StatusCode.Should().Be(HttpStatusCode.Forbidden, permissao.Codigo);
    }

    [Theory(DisplayName = "Sem login a resposta é 401")]
    [MemberData(nameof(Permissoes))]
    public async Task SemLogin_401(PermissaoConfiguracao permissao)
    {
        using HttpResponseMessage resposta = await EnviarAsync(null, permissao);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized, permissao.Codigo);
    }

    private async Task<HttpResponseMessage> EnviarAsync(string? sub, PermissaoConfiguracao permissao, string? papel = null)
    {
        bool consulta = permissao.Acao == "consultar";
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(
            consulta ? HttpMethod.Get : HttpMethod.Post,
            new Uri($"/api/configuracao/admin/{permissao.Recurso}", UriKind.Relative));
        if (!consulta)
        {
            request.Content = JsonContent.Create(new { });
        }

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
