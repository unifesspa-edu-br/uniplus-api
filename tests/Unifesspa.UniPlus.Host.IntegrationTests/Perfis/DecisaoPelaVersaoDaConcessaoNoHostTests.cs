namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// A decisão de acesso pela versão a que a concessão está presa, do início ao fim: o perfil nasce e muda de versão pela
/// API, o veredicto do documento chega pela fila do Wolverine, e a pessoa pede dois endpoints reais protegidos por
/// permissão. O endpoint do catálogo exige a primeira permissão e a manutenção dos motivos de isenção, a segunda; a
/// terceira só compõe versões. A decisão passa pelo resolvedor e pelo ponto de decisão do host.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class DecisaoPelaVersaoDaConcessaoNoHostTests : VersoesDoPerfilNoHostTestBase, IAsyncLifetime
{
    private const string Catalogo = "autorizacao:permissoes:consultar-catalogo";
    private const string Motivos = "selecao:motivos-decisao-isencao:manter";
    private const string Campi = "configuracao:campi:manter";

    public DecisaoPelaVersaoDaConcessaoNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    public async Task InitializeAsync()
    {
        await ResetarAsync();
        await ConcessoesDeTeste.RemoverDaPessoaAsync(Fixture.Factory.Services, Leandro.Sub.ToString());
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "A remoção numa versão nova vale na hora na decisão HTTP e leva a concessão para a versão nova")]
    public async Task Remocao_ValeNaHora()
    {
        using HttpClient gestor = await GestorAsync();
        Guid perfil = await PerfilComConcessaoAsync(gestor, Catalogo, Motivos);
        using HttpClient leandro = Cliente(Leandro);
        (await ConsultarCatalogoAsync(leandro)).Should().Be(HttpStatusCode.OK);
        (await ManterMotivosAsync(leandro)).Should().Be(HttpStatusCode.NotFound, "a permissão vale e o motivo sorteado não existe");

        Envio versao2 = await NovaVersaoAsync(gestor, perfil, Catalogo);

        (await ConsultarCatalogoAsync(leandro)).Should().Be(HttpStatusCode.OK);
        (await ManterMotivosAsync(leandro)).Should().Be(HttpStatusCode.Forbidden);
        (await ConcessoesAsync(Leandro, perfil)).Single().VersaoId.Should().Be(versao2.VersaoId);
    }

    [Fact(DisplayName = "O acréscimo numa versão nova não chega à concessão da versão antiga na decisão HTTP")]
    public async Task Acrescimo_NaoChegaAConcessaoAntiga()
    {
        using HttpClient gestor = await GestorAsync();
        Guid perfil = await PerfilComConcessaoAsync(gestor, Catalogo);
        using HttpClient leandro = Cliente(Leandro);
        Guid versao1 = (await ConcessoesAsync(Leandro, perfil)).Single().VersaoId!.Value;

        await NovaVersaoAsync(gestor, perfil, Catalogo, Motivos);

        (await ConsultarCatalogoAsync(leandro)).Should().Be(HttpStatusCode.OK);
        (await ManterMotivosAsync(leandro)).Should().Be(HttpStatusCode.Forbidden);
        (await ConcessoesAsync(Leandro, perfil)).Single().VersaoId.Should().Be(versao1);
    }

    [Fact(DisplayName = "A versão que só muda o nome leva todas as concessões para ela, sem tirar nem dar permissão")]
    public async Task SoNome_MigraTodos()
    {
        using HttpClient gestor = await GestorAsync();
        Guid perfil = await PerfilComConcessaoAsync(gestor, Catalogo, Motivos);
        await ConcessoesDeTeste.ConcederAsync(
            Fixture.Factory.Services, Betina.Sub.ToString(), perfil, EscopoDaConcessao.DaInstituicao(), email: Betina.Email);
        using HttpClient leandro = Cliente(Leandro);

        Envio versao2 = await NovaVersaoAsync(gestor, perfil, Catalogo, Motivos);

        (await ConsultarCatalogoAsync(leandro)).Should().Be(HttpStatusCode.OK);
        (await ManterMotivosAsync(leandro)).Should().Be(HttpStatusCode.NotFound);
        (await ConcessoesAsync(Leandro, perfil)).Single().VersaoId.Should().Be(versao2.VersaoId);
        (await ConcessoesAsync(Betina, perfil)).Single().VersaoId.Should().Be(versao2.VersaoId);
    }

    [Fact(DisplayName = "A permissão removida e devolvida em versão posterior não volta à concessão antiga, nem na decisão nem nos titulares")]
    public async Task RemocaoSeguidaDeRetorno_NaoVolta()
    {
        using HttpClient gestor = await GestorAsync();
        Guid perfil = await PerfilComConcessaoAsync(gestor, Catalogo, Motivos);
        using HttpClient leandro = Cliente(Leandro);
        Guid versao1 = (await ConcessoesAsync(Leandro, perfil)).Single().VersaoId!.Value;

        await NovaVersaoAsync(gestor, perfil, Catalogo, Motivos, Campi);
        await NovaVersaoAsync(gestor, perfil, Catalogo, Campi);
        await NovaVersaoAsync(gestor, perfil, Catalogo, Motivos, Campi);

        (await ConsultarCatalogoAsync(leandro)).Should().Be(HttpStatusCode.OK);
        (await ManterMotivosAsync(leandro)).Should().Be(HttpStatusCode.Forbidden);
        (await ConcessoesAsync(Leandro, perfil)).Single().VersaoId.Should().Be(versao1);
        (await TitularesAsync(Motivos)).Should().NotContain(Leandro.Email);
        (await TitularesAsync(Catalogo)).Should().Contain(Leandro.Email);
    }

    private async Task<Guid> PerfilComConcessaoAsync(HttpClient gestor, params string[] permissoes)
    {
        Envio envio = await CriarPerfilAsync(gestor, permissoes);
        await EnviarEVerificarPelaFilaAsync(gestor, envio);
        await ConcessoesDeTeste.ConcederAsync(
            Fixture.Factory.Services, Leandro.Sub.ToString(), envio.PerfilId, EscopoDaConcessao.DaInstituicao(), email: Leandro.Email);

        return envio.PerfilId;
    }

    private async Task<Envio> NovaVersaoAsync(HttpClient gestor, Guid perfil, params string[] permissoes)
    {
        Envio envio = await CriarVersaoAsync(gestor, perfil, Nome(), permissoes);
        await EnviarEVerificarPelaFilaAsync(gestor, envio);

        return envio;
    }

    private async Task EnviarEVerificarPelaFilaAsync(HttpClient gestor, Envio envio)
    {
        await ConfirmarEnvioAsync(gestor, envio);
        await EntregarPelaFilaAsync(envio.DocumentoId);
        (await TemVigenteComVersaoAsync(gestor, envio.PerfilId, envio.VersaoId)).Should().BeTrue("o veredicto verificado põe a versão em vigor");
    }

    private static async Task<HttpStatusCode> ConsultarCatalogoAsync(HttpClient cliente)
    {
        using HttpResponseMessage resposta = await cliente.GetAsync(new Uri("/api/autorizacao/permissoes", UriKind.Relative));

        return resposta.StatusCode;
    }

    private static async Task<HttpStatusCode> ManterMotivosAsync(HttpClient cliente)
    {
        using HttpResponseMessage resposta = await EnviarAsync(
            cliente, HttpMethod.Post, $"/api/selecao/admin/motivos-decisao-isencao/{Guid.NewGuid()}/ativacao");

        return resposta.StatusCode;
    }

    private async Task<IReadOnlyList<string>> TitularesAsync(string permissao)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<ITitularesDaPermissao>().ListarEmailsAsync(permissao);
    }
}
