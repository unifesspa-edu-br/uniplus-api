namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// A leitura do perfil, da listagem e do detalhe, é de quem mantém perfis ou aprova pela proteção de dados ou pela gestão de
/// acessos; a manutenção continua só de quem mantém perfis.
/// </summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class LeituraDoPerfilPorQuemAprovaEndpointTests : PerfilEndpointTestBase
{
    public LeituraDoPerfilPorQuemAprovaEndpointTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    // O perfil de governança da proteção de dados aprova versões de perfil e não mantém perfis.
    private async Task<HttpClient> ClienteDaProtecaoDeDadosAsync(string sub)
    {
        await ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, sub, TipoDeGovernanca.RepresentanteDoControlador);

        return Cliente(sub);
    }

    [Fact(DisplayName = "Quem só aprova pela proteção de dados lê a listagem e o detalhe do perfil")]
    public async Task Ler_SoComAprovacaoDaProtecaoDeDados_Devolve200()
    {
        using HttpClient fernanda = await ClienteDaProtecaoDeDadosAsync("fernanda.drumond");
        Guid id = await SemearPerfilVigenteAsync(ConsultarCatalogo);

        (await ListarTudoAsync(fernanda)).Should().Contain(i => i.GetProperty("id").GetGuid() == id);
        using JsonDocument detalhe = await LerAsync(fernanda, id);
        detalhe.RootElement.GetProperty("id").GetGuid().Should().Be(id);
    }

    [Fact(DisplayName = "Quem só aprova pela proteção de dados não mantém o perfil: desativar devolve 403")]
    public async Task Desativar_SoComAprovacaoDaProtecaoDeDados_Devolve403()
    {
        using HttpClient caroline = await ClienteDaProtecaoDeDadosAsync("caroline.lima");
        Guid id = await SemearPerfilVigenteAsync(ConsultarCatalogo);

        using HttpResponseMessage resposta = await EnviarAsync(
            caroline, HttpMethod.Post, $"{Rota}/{id}/desativacao", new { motivo = "Desativado para o teste." });

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "Sem manter nem aprovar perfis, a leitura do detalhe devolve 403")]
    public async Task Obter_SemManterNemAprovar_Devolve403()
    {
        using HttpClient manoel = await ClienteComPermissoesAsync("manoel.aparicio", ConsultarCatalogo);
        Guid id = await SemearPerfilVigenteAsync(ConsultarCatalogo);

        using HttpResponseMessage resposta = await manoel.GetAsync(new Uri($"{Rota}/{id}", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("\"uniplus.auth.forbidden\"");
    }

    [Fact(DisplayName = "Sem manter nem aprovar perfis, a listagem devolve 403")]
    public async Task Listar_SemManterNemAprovar_Devolve403()
    {
        using HttpClient marlene = await ClienteComPermissoesAsync("marlene.campos", ConsultarCatalogo);

        using HttpResponseMessage resposta = await marlene.GetAsync(new Uri(Rota, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("\"uniplus.auth.forbidden\"");
    }
}
