namespace Unifesspa.UniPlus.Host.IntegrationTests.Revisoes;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Quem decide o item da revisão se avalia no instante da decisão, e não na abertura (ADR-0162): a designação de
/// Representante do controlador concedida, revogada ou vencida com a revisão aberta muda o decisor a partir dali, e a
/// decisão grava a governança com que foi tomada. Marlene é Representante e conduz; Diogo e Betina são gestores; Leandro
/// tem o acesso sensível a rever.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class RevisaoComDesignacaoDuranteTests : PedidosDeConcessaoTestBase
{
    private const string RotaDasRevisoes = "/api/autorizacao/revisoes";
    private const string Motivo = "Leandro deixou a função de Representante.";
    private const string Gestor = "gestor-de-acessos";
    private const string Representante = "representante-do-controlador";

    public RevisaoComDesignacaoDuranteTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A designação concedida com a revisão aberta passa o item aos gestores, e a decisão grava a governança dos gestores")]
    public async Task DesignacaoConcedidaDurante_PassaOItemAosGestores()
    {
        Revisao revisao = await AbrirComLeandroAsync(leandroERepresentante: false);
        (await DecideAsync(revisao)).Should().Be(Representante);

        Guid designacao = await DesignarLeandroAsync();

        (await DecideAsync(revisao)).Should().Be(Gestor);
        await ConferirRecusaDoItemAsync(await ConfirmarAsync(revisao.Marlene, revisao));
        (await ConfirmarAsync(revisao.Diogo, revisao)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await VencerAsync(designacao);
        (await DecideAsync(revisao)).Should().Be(Gestor);
    }

    [Fact(DisplayName = "A designação revogada com a revisão aberta devolve o item ao Representante, e os gestores já não o decidem")]
    public async Task DesignacaoRevogadaDurante_DevolveOItemAoRepresentante()
    {
        Revisao revisao = await AbrirComLeandroAsync(leandroERepresentante: true);
        (await DecideAsync(revisao)).Should().Be(Gestor);

        (await RevogarAsync(revisao.Diogo, revisao.DesignacaoDeLeandro!.Value, Motivo)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await ConferirDecisaoDoRepresentanteAsync(revisao);
    }

    [Fact(DisplayName = "A designação vencida com a revisão aberta devolve o item ao Representante: a vencida não conta como vigente")]
    public async Task DesignacaoVencidaDurante_DevolveOItemAoRepresentante()
    {
        Revisao revisao = await AbrirComLeandroAsync(leandroERepresentante: true);

        await VencerAsync(revisao.DesignacaoDeLeandro!.Value);

        await ConferirDecisaoDoRepresentanteAsync(revisao);
    }

    // Depois da decisão, Leandro volta a ser Representante: o item decidido conserva a governança gravada na decisão, e
    // não a condição atual do beneficiário.
    private async Task ConferirDecisaoDoRepresentanteAsync(Revisao revisao)
    {
        (await DecideAsync(revisao)).Should().Be(Representante);
        await ConferirRecusaDoItemAsync(await ConfirmarAsync(revisao.Diogo, revisao));
        (await ConfirmarAsync(revisao.Marlene, revisao)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await TornarLeandroRepresentanteVigenteAsync();
        (await DecideAsync(revisao)).Should().Be(Representante);
    }

    private Task<Guid> DesignarLeandroAsync() =>
        ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, Leandro.Sub.ToString(), TipoDeGovernanca.RepresentanteDoControlador);

    // A designação vencida não revogada é reaproveitada pelo apoio de teste; sem prazo, ela volta a ser vigente.
    private async Task TornarLeandroRepresentanteVigenteAsync() =>
        await ExecutarSqlAsync("UPDATE autorizacao.concessoes SET valida_ate = NULL WHERE id = {0}", await DesignarLeandroAsync());

    private Task VencerAsync(Guid designacao) =>
        ExecutarSqlAsync(
            "UPDATE autorizacao.concessoes SET concedida_em = now() - interval '3 days', valida_ate = now() - interval '1 minute' WHERE id = {0}",
            designacao);

    // Leandro tem o acesso sensível e, se pedido, também a designação de Representante; Marlene abre a revisão.
    private async Task<Revisao> AbrirComLeandroAsync(bool leandroERepresentante)
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilSensivelAsync();
        await SemearConcessaoAsync(Leandro, perfil, EscopoDaConcessao.DaUnidade(await SemearUnidadeAsync()));
        Guid? designacao = leandroERepresentante
            ? await DesignarLeandroAsync()
            : null;
        HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);
        HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);

        JsonElement aberta = await CorpoAsync(await EnviarAsync(marlene, HttpMethod.Post, RotaDasRevisoes), HttpStatusCode.Created);
        return new Revisao(aberta.GetProperty("id").GetGuid(), ItemSensivelDeLeandro(aberta, perfil).GetProperty("id").GetGuid(), perfil, designacao, marlene, diogo);
    }

    private static JsonElement ItemSensivelDeLeandro(JsonElement revisao, Guid perfil) =>
        revisao.GetProperty("itens").EnumerateArray().Single(i =>
            i.GetProperty("beneficiario").GetProperty("sub").GetString() == Leandro.Sub.ToString()
            && i.GetProperty("perfil").GetProperty("id").GetGuid() == perfil);

    private static async Task<string?> DecideAsync(Revisao revisao) =>
        ItemSensivelDeLeandro(await LerAsync(revisao.Marlene, $"{RotaDasRevisoes}/{revisao.Id}"), revisao.Perfil).GetProperty("decide").GetString();

    private static Task<HttpResponseMessage> ConfirmarAsync(HttpClient decisor, Revisao revisao) =>
        EnviarAsync(decisor, HttpMethod.Post, $"{RotaDasRevisoes}/{revisao.Id}/itens/{revisao.ItemId}/confirmacao");

    private static async Task ConferirRecusaDoItemAsync(HttpResponseMessage resposta)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, corpo);
        corpo.Should().Contain("uniplus.autorizacao.revisao.decisor_nao_decide_o_item");
    }

    private sealed record Revisao(Guid Id, Guid ItemId, Guid Perfil, Guid? DesignacaoDeLeandro, HttpClient Marlene, HttpClient Diogo);
}
