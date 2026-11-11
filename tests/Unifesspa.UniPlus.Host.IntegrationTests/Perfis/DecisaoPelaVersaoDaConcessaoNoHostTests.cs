namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

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
    private const string Motivos = "selecao:motivos-de-decisao:manter";
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

    [Fact(DisplayName = "A remoção vale na hora na decisão HTTP e leva a concessão para a versão nova")]
    public async Task Remocao_ValeNaHora()
    {
        using HttpClient gestor = await GestorAsync();
        Guid perfil = await PerfilComConcessaoAsync(gestor, Catalogo, Motivos);
        using HttpClient leandro = Cliente(Leandro);
        (await ConsultarCatalogoAsync(leandro)).Should().Be(HttpStatusCode.OK);
        (await ManterMotivosAsync(leandro)).Should().Be(HttpStatusCode.NotFound, "a permissão vale e o motivo sorteado não existe");

        Guid versao2 = await RemoverAsync(gestor, perfil, Motivos);

        (await ConsultarCatalogoAsync(leandro)).Should().Be(HttpStatusCode.OK);
        (await ManterMotivosAsync(leandro)).Should().Be(HttpStatusCode.Forbidden);
        (await ConcessoesAsync(Leandro, perfil)).Single().VersaoId.Should().Be(versao2);
    }

    [Fact(DisplayName = "O acréscimo a perfil com concessão cria a versão pendente e a concessão fica como estava até migrar")]
    public async Task Acrescimo_CriaAPendenteEAConcessaoFicaComoEstava()
    {
        using HttpClient gestor = await GestorAsync();
        Guid perfil = await PerfilComConcessaoAsync(gestor, Catalogo);
        using HttpClient leandro = Cliente(Leandro);
        Guid versao1 = (await ConcessoesAsync(Leandro, perfil)).Single().VersaoId!.Value;

        using HttpResponseMessage recusa = await EnviarAsync(
            gestor, HttpMethod.Post, $"{RotaPerfis}/{perfil}/versoes", Dados(Nome(), Catalogo, Motivos));

        recusa.StatusCode.Should().Be(HttpStatusCode.Created, await recusa.Content.ReadAsStringAsync());
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

        Envio versao2 = await CriarVersaoAsync(gestor, perfil, Nome(), Catalogo, Motivos);
        await EnviarEVerificarPelaFilaAsync(gestor, versao2);

        (await ConsultarCatalogoAsync(leandro)).Should().Be(HttpStatusCode.OK);
        (await ManterMotivosAsync(leandro)).Should().Be(HttpStatusCode.NotFound);
        (await ConcessoesAsync(Leandro, perfil)).Single().VersaoId.Should().Be(versao2.VersaoId);
        (await ConcessoesAsync(Betina, perfil)).Single().VersaoId.Should().Be(versao2.VersaoId);
    }

    [Fact(DisplayName = "A permissão removida não volta à concessão enquanto a proposta que a devolve não entra em vigor, nem na decisão nem nos titulares")]
    public async Task RemocaoSeguidaDeRetorno_NaoVolta()
    {
        using HttpClient gestor = await GestorAsync();
        Guid perfil = await PerfilComConcessaoAsync(gestor, Catalogo, Motivos);
        using HttpClient leandro = Cliente(Leandro);
        Guid versao2 = await RemoverAsync(gestor, perfil, Motivos);

        using HttpResponseMessage devolucao = await EnviarAsync(
            gestor, HttpMethod.Post, $"{RotaPerfis}/{perfil}/versoes", Dados(Nome(), Catalogo, Motivos));

        devolucao.StatusCode.Should().Be(HttpStatusCode.Created, await devolucao.Content.ReadAsStringAsync());
        (await ConsultarCatalogoAsync(leandro)).Should().Be(HttpStatusCode.OK);
        (await ManterMotivosAsync(leandro)).Should().Be(HttpStatusCode.Forbidden);
        (await ConcessoesAsync(Leandro, perfil)).Single().VersaoId.Should().Be(versao2);
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

    private static async Task<Guid> RemoverAsync(HttpClient gestor, Guid perfil, params string[] permissoes)
    {
        using HttpResponseMessage resposta = await RemoverBrutoAsync(gestor, perfil, permissoes);
        JsonElement corpo = await CorpoAsync(resposta, HttpStatusCode.OK);

        return corpo.GetProperty("composicaoEmVigor").GetProperty("versaoId").GetGuid();
    }

    private async Task EnviarEVerificarPelaFilaAsync(HttpClient gestor, Envio envio)
    {
        await ConfirmarEnvioAsync(gestor, envio);
        await EntregarPelaFilaAsync(envio.DocumentoId);
        await AprovarAteAVigenciaAsync(gestor, envio);
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
