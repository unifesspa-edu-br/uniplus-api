namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// A suspensão do perfil pela API real: o acesso das concessões some sem que elas sejam revogadas e volta com a
/// retomada, que pede a gestão de acessos e, no perfil protegido, a proteção de dados.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class ContencaoDoPerfilNoHostTests : ContencaoDoPerfilNoHostTestBase
{
    public ContencaoDoPerfilNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A suspensão bloqueia o acesso sem revogar a concessão, e a retomada da gestão o devolve no perfil comum")]
    public async Task Suspensao_BloqueiaEARetomadaRestitui()
    {
        await PrepararAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient isabella = await GestaoAsync();
        using HttpClient leandro = Cliente(Leandro);
        Guid perfilId = await PerfilComumComConcessaoAsync(diogo);
        (await ConsultarCatalogoAsync(leandro)).Should().Be(HttpStatusCode.OK);

        await SuspenderComSucessoAsync(diogo, perfilId);

        (await ConsultarCatalogoAsync(leandro)).Should().Be(HttpStatusCode.Forbidden);
        (await ConcessoesAsync(Leandro, perfilId)).Should().ContainSingle().Which.RevogadaEm.Should().BeNull();
        JsonElement suspenso = await PerfilAsync(diogo, perfilId);
        suspenso.GetProperty("suspensao").GetProperty("papelDeQuemSuspendeu").GetString().Should().Be(EtapaGestao);
        suspenso.GetProperty("suspensao").GetProperty("etapasExigidasAgora").EnumerateArray().Select(e => e.GetString()).Should().Equal(EtapaGestao);

        JsonElement retomada = await RetomarComSucessoAsync(isabella, perfilId, EtapaGestao);

        retomada.GetProperty("concluida").GetBoolean().Should().BeTrue();
        (await ConsultarCatalogoAsync(leandro)).Should().Be(HttpStatusCode.OK);
        (await PerfilAsync(diogo, perfilId)).GetProperty("suspensao").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact(DisplayName = "No perfil protegido, só a gestão não devolve o acesso; a proteção de dados também decide e a retomada se fecha")]
    public async Task Suspensao_PerfilProtegido_PedeAsDuasEtapas()
    {
        await PrepararAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient isabella = await GestaoAsync();
        using HttpClient marlene = await ProtecaoAsync();
        Guid perfilId = await PerfilProtegidoComConcessaoAsync(diogo);
        await SuspenderComSucessoAsync(diogo, perfilId);

        JsonElement primeira = await RetomarComSucessoAsync(isabella, perfilId, EtapaGestao);

        primeira.GetProperty("concluida").GetBoolean().Should().BeFalse();
        primeira.GetProperty("etapasPendentes").EnumerateArray().Select(e => e.GetString()).Should().Equal(EtapaProtecao);
        (await PermissoesDaConcessaoAsync(perfilId)).Should().BeEmpty();

        JsonElement segunda = await RetomarComSucessoAsync(marlene, perfilId, EtapaProtecao);

        segunda.GetProperty("concluida").GetBoolean().Should().BeTrue();
        (await PermissoesDaConcessaoAsync(perfilId)).Should().BeEquivalentTo(Consultar, Sensivel);
    }

    [Fact(DisplayName = "A concessão criada durante a suspensão nasce bloqueada, e os titulares da permissão não incluem o perfil suspenso")]
    public async Task Suspensao_ConcessaoNovaBloqueada_ETitularesSemOPerfil()
    {
        await PrepararAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient isabella = await GestaoAsync();
        using HttpClient betina = Cliente(Betina);
        Guid perfilId = await PerfilComumComConcessaoAsync(diogo);
        await SuspenderComSucessoAsync(diogo, perfilId);

        await ConcessoesDeTeste.ConcederAsync(
            Fixture.Factory.Services, Betina.Sub.ToString(), perfilId, EscopoDaConcessao.DaInstituicao(), email: Betina.Email);

        (await ConsultarCatalogoAsync(betina)).Should().Be(HttpStatusCode.Forbidden);
        (await TitularesAsync(Catalogo)).Should().NotContain(Leandro.Email).And.NotContain(Betina.Email);

        await RetomarComSucessoAsync(isabella, perfilId, EtapaGestao);

        (await ConsultarCatalogoAsync(betina)).Should().Be(HttpStatusCode.OK);
        (await TitularesAsync(Catalogo)).Should().Contain(Leandro.Email).And.Contain(Betina.Email);
    }

    [Fact(DisplayName = "Desativar não muda o acesso nem a composição; reativar exige outra pessoa e os dois ficam na trilha")]
    public async Task Desativacao_NaoMudaOAcesso_ReativacaoExigeOutraPessoa()
    {
        await PrepararAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient isabella = await GestaoAsync();
        using HttpClient leandro = Cliente(Leandro);
        Guid perfilId = await PerfilComumComConcessaoAsync(diogo);

        using HttpResponseMessage desativada = await DesativarAsync(diogo, perfilId);

        desativada.StatusCode.Should().Be(HttpStatusCode.NoContent, await desativada.Content.ReadAsStringAsync());
        (await ConsultarCatalogoAsync(leandro)).Should().Be(HttpStatusCode.OK);
        JsonElement perfil = await PerfilAsync(diogo, perfilId);
        perfil.GetProperty("ativo").GetBoolean().Should().BeFalse();
        perfil.GetProperty("desativadoDesde").ValueKind.Should().Be(JsonValueKind.String);
        using HttpResponseMessage pelaMesmaPessoa = await ReativarAsync(diogo, perfilId);
        await ConferirRecusaDoPerfilAsync(pelaMesmaPessoa, HttpStatusCode.UnprocessableEntity, "reativacao_por_quem_desativou");

        using HttpResponseMessage reativada = await ReativarAsync(isabella, perfilId);

        reativada.StatusCode.Should().Be(HttpStatusCode.NoContent, await reativada.Content.ReadAsStringAsync());
        (await PerfilAsync(diogo, perfilId)).GetProperty("ativo").GetBoolean().Should().BeTrue();
        (await TiposDaContencaoAsync(diogo, perfilId)).Should().Equal("desativacao", "reativacao");
    }

    [Fact(DisplayName = "A trilha traz suspensão, decisão de cada etapa e retomada em ordem crescente, com a retomada atribuída ao sistema")]
    public async Task Trilha_TrazOsRegistrosEmOrdem()
    {
        await PrepararAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient isabella = await GestaoAsync();
        using HttpClient marlene = await ProtecaoAsync();
        Guid perfilId = await PerfilProtegidoComConcessaoAsync(diogo);
        await SuspenderComSucessoAsync(diogo, perfilId);
        await RetomarComSucessoAsync(marlene, perfilId, EtapaProtecao);
        await RetomarComSucessoAsync(isabella, perfilId, EtapaGestao);

        JsonElement[] trilha = [.. (await TrilhaAsync(diogo, perfilId)).EnumerateArray()];

        trilha.Select(r => r.GetProperty("tipo").GetString()).Should().Equal("suspensao", "decisao_da_retomada", "decisao_da_retomada", "retomada");
        trilha.Select(r => r.GetProperty("ordem").GetInt64()).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        trilha[0].GetProperty("pessoaNome").GetString().Should().Be(Diogo.Nome);
        trilha[1].GetProperty("etapa").GetString().Should().Be(EtapaProtecao);
        trilha[3].GetProperty("ator").GetString().Should().Be("sistema");
        trilha[3].GetProperty("origem").GetString().Should().Be("decisao_da_etapa");
        trilha[3].GetProperty("pessoaNome").ValueKind.Should().Be(JsonValueKind.Null);
        trilha.Select(r => r.GetProperty("referenciaId").GetGuid()).Distinct().Should().ContainSingle();
    }

    [Fact(DisplayName = "A suspensão avisa quem mantém perfis e a proteção de dados, sem motivo nem nome, e não avisa quem suspendeu")]
    public async Task Suspensao_AvisaOsOutrosGestores()
    {
        await PrepararAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient isabella = await GestaoAsync();
        using HttpClient marlene = await ProtecaoAsync();
        Guid perfilId = await PerfilComumComConcessaoAsync(diogo);

        await SuspenderComSucessoAsync(diogo, perfilId);

        JsonElement paraAGestao = await AguardarAvisoAsync(Isabella, perfilId, AssuntoDaSuspensao);
        await AguardarAvisoAsync(Marlene, perfilId, AssuntoDaSuspensao);
        paraAGestao.GetProperty("Text").GetString().Should().NotContain(Motivo).And.NotContain(Diogo.Nome).And.NotContain(Diogo.Email);
        (await MensagensAsync(Diogo, perfilId)).Should().BeEmpty("quem suspendeu não é avisado");
    }

    [Fact(DisplayName = "Quem não mantém perfis nem aprova pela proteção de dados é negado em todas as operações da suspensão")]
    public async Task SemPermissao_Negado()
    {
        await PrepararAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient leandro = Cliente(Leandro);
        Guid perfilId = await PerfilComumComConcessaoAsync(diogo);

        using HttpResponseMessage suspensao = await SuspenderAsync(leandro, perfilId);
        using HttpResponseMessage retomada = await RetomarAsync(leandro, perfilId, EtapaGestao);
        using HttpResponseMessage trilha = await leandro.GetAsync(new Uri($"{RotaPerfis}/{perfilId}/contencao", UriKind.Relative));
        using HttpResponseMessage desativacao = await DesativarAsync(leandro, perfilId);

        new[] { suspensao, retomada, trilha, desativacao }.Select(r => r.StatusCode).Should().AllBeEquivalentTo(HttpStatusCode.Forbidden);
        (await PerfilAsync(diogo, perfilId)).GetProperty("suspensao").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact(DisplayName = "As regras da retomada chegam como erro tipado: quem suspendeu, etapa repetida, mesma pessoa e sem suspensão")]
    public async Task Retomada_RecusasTipadas()
    {
        await PrepararAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient isabella = await GestoraComAmbasAsync();
        Guid perfilId = await PerfilProtegidoComConcessaoAsync(diogo);
        using HttpResponseMessage semSuspensao = await RetomarAsync(isabella, perfilId, EtapaGestao);
        await ConferirRecusaDoPerfilAsync(semSuspensao, HttpStatusCode.UnprocessableEntity, "nao_suspenso");
        await SuspenderComSucessoAsync(diogo, perfilId);

        using HttpResponseMessage pelaPessoaQueSuspendeu = await RetomarAsync(diogo, perfilId, EtapaGestao);
        await RetomarComSucessoAsync(isabella, perfilId, EtapaGestao);
        using HttpResponseMessage repetida = await RetomarAsync(isabella, perfilId, EtapaGestao);
        using HttpResponseMessage mesmaPessoa = await RetomarAsync(isabella, perfilId, EtapaProtecao);

        await ConferirRecusaDoPerfilAsync(pelaPessoaQueSuspendeu, HttpStatusCode.UnprocessableEntity, "retomada_por_quem_suspendeu");
        await ConferirRecusaDoPerfilAsync(repetida, HttpStatusCode.Conflict, "etapa_da_retomada_ja_decidida");
        await ConferirRecusaDoPerfilAsync(mesmaPessoa, HttpStatusCode.UnprocessableEntity, "mesmo_decisor_nas_duas_etapas");
    }

    [Fact(DisplayName = "Suspender sem motivo, o perfil de governança e o perfil inexistente são recusados")]
    public async Task Suspender_Recusas()
    {
        await PrepararAsync();
        using HttpClient diogo = await GestorAsync();
        Guid perfilId = await PerfilComumComConcessaoAsync(diogo);

        using HttpResponseMessage semMotivo = await SuspenderAsync(diogo, perfilId, motivo: "  ");
        using HttpResponseMessage governanca = await SuspenderAsync(diogo, PerfisDeSistema.GestorDeAcessosId);
        using HttpResponseMessage inexistente = await SuspenderAsync(diogo, Guid.NewGuid());

        await ConferirRecusaDoPerfilAsync(semMotivo, HttpStatusCode.UnprocessableEntity, "motivo_invalido");
        await ConferirRecusaDoPerfilAsync(governanca, HttpStatusCode.UnprocessableEntity, "governanca_imutavel");
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
