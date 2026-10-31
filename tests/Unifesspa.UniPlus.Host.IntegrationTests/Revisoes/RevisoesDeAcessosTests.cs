namespace Unifesspa.UniPlus.Host.IntegrationTests.Revisoes;

using System.Diagnostics.CodeAnalysis;
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
/// A revisão periódica dos acessos pelo host real (ADR-0152). Marlene é da proteção de dados e conduz a revisão; Diogo,
/// Isabella e Betina são gestores de acessos; Leandro tem o acesso sensível a rever.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class RevisoesDeAcessosTests : PedidosDeConcessaoTestBase
{
    private const string RotaDasRevisoes = "/api/autorizacao/revisoes";
    private const string Motivo = "O atendimento do edital terminou.";

    public RevisoesDeAcessosTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A revisão traz só os acessos pessoais ou sensíveis, manda os da proteção de dados aos gestores e abre uma por vez")]
    public async Task Abrir_TrazSoPessoaisOuSensiveis()
    {
        await ResetarAsync();
        Guid unidade = await SemearUnidadeAsync();
        await SemearConcessaoAsync(Leandro, await SemearPerfilSensivelAsync(), EscopoDaConcessao.DaUnidade(unidade));
        await SemearConcessaoAsync(Betina, await SemearPerfilInternoAsync(), EscopoDaConcessao.DaUnidade(unidade));
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);

        (_, JsonElement revisao) = await AbrirAsync(marlene);

        ItensDe(revisao, Betina).Should().BeEmpty("o perfil só interno não entra");
        ItemDe(revisao, Leandro).GetProperty("decide").GetString().Should().Be("protecao-de-dados");
        ItemDe(revisao, Leandro).GetProperty("situacao").GetString().Should().Be("pendente");
        ItemDe(revisao, Marlene).GetProperty("decide").GetString().Should().Be("gestor-de-acessos");
        ItemDe(revisao, Diogo).GetProperty("decide").GetString().Should().Be("protecao-de-dados");
        revisao.GetProperty("situacao").GetString().Should().Be("em_andamento");

        await ConferirErroAsync(
            await EnviarAsync(marlene, HttpMethod.Post, RotaDasRevisoes), HttpStatusCode.Conflict, "revisao.ja_ha_em_andamento");
        using HttpClient leandro = Cliente(Leandro);
        (await leandro.GetAsync(new Uri(RotaDasRevisoes, UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await EnviarAsync(diogo, HttpMethod.Post, RotaDasRevisoes)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "Revogar na revisão encerra o acesso na próxima requisição e entra na trilha da concessão")]
    public async Task Revogar_EncerraOAcessoERegistraNaTrilha()
    {
        await ResetarAsync();
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        using HttpClient isabella = await ClienteComPermissoesAsync(Isabella, AprovarFuncional);
        using HttpClient betina = await ClienteComPermissoesAsync(Betina, AprovarFuncional);
        (_, JsonElement revisao) = await AbrirAsync(marlene);
        Guid revisaoId = revisao.GetProperty("id").GetGuid();
        JsonElement item = ItemDe(revisao, Diogo);
        Guid itemId = item.GetProperty("id").GetGuid();
        Guid concessaoId = item.GetProperty("concessaoId").GetGuid();
        (await diogo.GetAsync(new Uri("/api/autorizacao/concessoes", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.OK);

        await ConferirErroAsync(
            await EnviarAsync(marlene, HttpMethod.Post, $"{RotaDasRevisoes}/{revisaoId}/itens/{itemId}/revogacao", new { motivo = " " }),
            HttpStatusCode.UnprocessableEntity,
            "concessao.motivo_invalido");
        (await EnviarAsync(marlene, HttpMethod.Post, $"{RotaDasRevisoes}/{revisaoId}/itens/{itemId}/revogacao", new { motivo = Motivo }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await diogo.GetAsync(new Uri("/api/autorizacao/concessoes", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        JsonElement depois = await LerAsync(marlene, $"{RotaDasRevisoes}/{revisaoId}");
        ItemDe(depois, Diogo).GetProperty("situacao").GetString().Should().Be("revogada");
        ItemDe(depois, Diogo).GetProperty("decididoPor").GetProperty("sub").GetString().Should().Be(Marlene.Sub.ToString());
        JsonElement trilha = await LerAsync(isabella, $"/api/autorizacao/concessoes/{concessaoId}/trilha");
        string[] tipos = [.. trilha.EnumerateArray().Select(e => e.GetProperty("tipo").GetString()!)];
        tipos.Should().Contain(["revogada", "revisao_revogada"]);
        await ConferirErroAsync(
            await EnviarAsync(marlene, HttpMethod.Post, $"{RotaDasRevisoes}/{revisaoId}/itens/{itemId}/confirmacao"),
            HttpStatusCode.Conflict,
            "revisao.item_ja_decidido");
    }

    [Fact(DisplayName = "Revogar o penúltimo gestor pela revisão é recusado e o item segue pendente")]
    public async Task Revogar_PenultimoGestor_RespeitaOMinimoDeGestores()
    {
        await ResetarAsync();
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        using HttpClient betina = await ClienteComPermissoesAsync(Betina, AprovarFuncional);
        (Guid revisaoId, JsonElement revisao) = await AbrirAsync(marlene);
        Guid itemId = ItemDe(revisao, Diogo).GetProperty("id").GetGuid();

        await ConferirErroAsync(
            await EnviarAsync(marlene, HttpMethod.Post, $"{RotaDasRevisoes}/{revisaoId}/itens/{itemId}/revogacao", new { motivo = Motivo }),
            HttpStatusCode.UnprocessableEntity,
            "concessao.minimo_de_gestores");

        ItemDe(await LerAsync(marlene, $"{RotaDasRevisoes}/{revisaoId}"), Diogo).GetProperty("situacao").GetString().Should().Be("pendente");
        (await diogo.GetAsync(new Uri("/api/autorizacao/concessoes", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "O item da proteção de dados só é decidido pelos gestores, e a confirmação entra na trilha sem encerrar o acesso")]
    public async Task ItemDaProtecaoDeDados_SoOsGestoresDecidem()
    {
        await ResetarAsync();
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        (Guid revisaoId, JsonElement revisao) = await AbrirAsync(marlene);
        JsonElement item = ItemDe(revisao, Marlene);
        string rota = $"{RotaDasRevisoes}/{revisaoId}/itens/{item.GetProperty("id").GetGuid()}/confirmacao";

        await ConferirErroAsync(await EnviarAsync(marlene, HttpMethod.Post, rota), HttpStatusCode.UnprocessableEntity, "revisao.decisor_nao_decide_o_item");
        (await EnviarAsync(diogo, HttpMethod.Post, rota)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        JsonElement trilha = await LerAsync(diogo, $"/api/autorizacao/concessoes/{item.GetProperty("concessaoId").GetGuid()}/trilha");
        trilha.EnumerateArray().Select(e => e.GetProperty("tipo").GetString()).Should().Contain("revisao_confirmada").And.NotContain("revogada");
    }

    [Fact(DisplayName = "O gestor de acessos não decide o item que cabe à proteção de dados, e a concessão segue ativa")]
    public async Task ItemComum_GestorNaoDecide()
    {
        await ResetarAsync();
        Guid unidade = await SemearUnidadeAsync();
        await SemearConcessaoAsync(Leandro, await SemearPerfilSensivelAsync(), EscopoDaConcessao.DaUnidade(unidade));
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        (Guid revisaoId, JsonElement revisao) = await AbrirAsync(marlene);
        string rotaDoItem = $"{RotaDasRevisoes}/{revisaoId}/itens/{ItemDe(revisao, Leandro).GetProperty("id").GetGuid()}";

        await ConferirErroAsync(
            await EnviarAsync(diogo, HttpMethod.Post, $"{rotaDoItem}/confirmacao"), HttpStatusCode.UnprocessableEntity, "revisao.decisor_nao_decide_o_item");
        await ConferirErroAsync(
            await EnviarAsync(diogo, HttpMethod.Post, $"{rotaDoItem}/revogacao", new { motivo = Motivo }),
            HttpStatusCode.UnprocessableEntity,
            "revisao.decisor_nao_decide_o_item");

        JsonElement item = ItemDe(await LerAsync(marlene, $"{RotaDasRevisoes}/{revisaoId}"), Leandro);
        item.GetProperty("situacao").GetString().Should().Be("pendente");
        JsonElement trilha = await LerAsync(diogo, $"/api/autorizacao/concessoes/{item.GetProperty("concessaoId").GetGuid()}/trilha");
        trilha.EnumerateArray().Select(e => e.GetProperty("tipo").GetString()).Should().NotContain("revogada");
    }

    [Fact(DisplayName = "O acesso comum de quem é da proteção de dados é revisto pelos gestores, e a proteção de dados não o decide")]
    public async Task AcessoComumDaProtecaoDeDados_VaiParaOsGestores()
    {
        await ResetarAsync();
        Guid unidade = await SemearUnidadeAsync();
        await ConcessoesDeTeste.ConcederAsync(
            Fixture.Factory.Services, Marlene.Sub.ToString(), await SemearPerfilSensivelAsync(), EscopoDaConcessao.DaUnidade(unidade));
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        (Guid revisaoId, JsonElement revisao) = await AbrirAsync(marlene);

        JsonElement[] itens = ItensDe(revisao, Marlene);
        itens.Should().HaveCount(2).And.OnlyContain(i => i.GetProperty("decide").GetString() == "gestor-de-acessos");
        JsonElement comum = itens.Single(i => i.GetProperty("perfil").GetProperty("nome").GetString()!.StartsWith("Análise", StringComparison.Ordinal));
        string rota = $"{RotaDasRevisoes}/{revisaoId}/itens/{comum.GetProperty("id").GetGuid()}/confirmacao";

        await ConferirErroAsync(await EnviarAsync(marlene, HttpMethod.Post, rota), HttpStatusCode.UnprocessableEntity, "revisao.decisor_nao_decide_o_item");
        (await EnviarAsync(diogo, HttpMethod.Post, rota)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "A listagem traz a situação e a contagem de pendentes, e só quem tem permissão da revisão a lê")]
    public async Task Listar_TrazSituacaoEPendentes()
    {
        await ResetarAsync();
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        (Guid revisaoId, JsonElement aberta) = await AbrirAsync(marlene);

        JsonElement pagina = await LerAsync(diogo, RotaDasRevisoes);
        JsonElement itens = pagina.ValueKind == JsonValueKind.Array ? pagina : pagina.GetProperty("items");
        JsonElement resumo = itens.EnumerateArray().Single(r => r.GetProperty("id").GetGuid() == revisaoId);
        resumo.GetProperty("situacao").GetString().Should().Be("em_andamento");
        resumo.GetProperty("totalDeItens").GetInt32().Should().Be(aberta.GetProperty("totalDeItens").GetInt32());
        resumo.GetProperty("pendentes").GetInt32().Should().Be(aberta.GetProperty("pendentes").GetInt32()).And.BePositive();
        using HttpClient leandro = Cliente(Leandro);
        (await leandro.GetAsync(new Uri($"{RotaDasRevisoes}/{revisaoId}", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "Decidir revisão ou item inexistente responde 404")]
    public async Task Decidir_RevisaoOuItemInexistente_Responde404()
    {
        await ResetarAsync();
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);
        (Guid revisaoId, _) = await AbrirAsync(marlene);

        await ConferirErroAsync(
            await EnviarAsync(marlene, HttpMethod.Post, $"{RotaDasRevisoes}/{Guid.NewGuid()}/itens/{Guid.NewGuid()}/confirmacao"),
            HttpStatusCode.NotFound,
            "revisao.nao_encontrada");
        await ConferirErroAsync(
            await EnviarAsync(marlene, HttpMethod.Post, $"{RotaDasRevisoes}/{revisaoId}/itens/{Guid.NewGuid()}/revogacao", new { motivo = Motivo }),
            HttpStatusCode.NotFound,
            "revisao.item_nao_encontrado");
        await ConferirErroAsync(
            await marlene.GetAsync(new Uri($"{RotaDasRevisoes}/{Guid.NewGuid()}", UriKind.Relative)),
            HttpStatusCode.NotFound,
            "revisao.nao_encontrada");
    }

    [Fact(DisplayName = "Concessão encerrada fora da revisão deixa de ser pendente e não aceita decisão")]
    public async Task ConcessaoEncerradaForaDaRevisao_NaoPedeDecisao()
    {
        await ResetarAsync();
        Guid unidade = await SemearUnidadeAsync();
        await ConcessoesDeTeste.ConcederAsync(
            Fixture.Factory.Services, Leandro.Sub.ToString(), await SemearPerfilSensivelAsync(), EscopoDaConcessao.DaUnidade(unidade));
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        using HttpClient betina = await ClienteComPermissoesAsync(Betina, AprovarFuncional);
        (Guid revisaoId, JsonElement revisao) = await AbrirAsync(marlene);
        int pendentes = revisao.GetProperty("pendentes").GetInt32();
        JsonElement item = ItemDe(revisao, Leandro);

        Guid concessaoId = item.GetProperty("concessaoId").GetGuid();
        (await EnviarAsync(diogo, HttpMethod.Post, $"/api/autorizacao/concessoes/{concessaoId}/revogacao", new { motivo = Motivo, confirmacaoEmail = ConcessoesDeTeste.EmailDeTeste }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        JsonElement depois = await LerAsync(marlene, $"{RotaDasRevisoes}/{revisaoId}");
        ItemDe(depois, Leandro).GetProperty("situacao").GetString().Should().Be("concessao_encerrada");
        depois.GetProperty("pendentes").GetInt32().Should().Be(pendentes - 1);
        await ConferirErroAsync(
            await EnviarAsync(marlene, HttpMethod.Post, $"{RotaDasRevisoes}/{revisaoId}/itens/{item.GetProperty("id").GetGuid()}/confirmacao"),
            HttpStatusCode.Conflict,
            "revisao.concessao_nao_ativa");
    }

    private static JsonElement[] ItensDe(JsonElement revisao, Persona persona) =>
        [.. revisao.GetProperty("itens").EnumerateArray()
            .Where(i => i.GetProperty("beneficiario").GetProperty("sub").GetString() == persona.Sub.ToString())];

    private static JsonElement ItemDe(JsonElement revisao, Persona persona) => ItensDe(revisao, persona).Single();

    private static async Task ConferirErroAsync(HttpResponseMessage resposta, HttpStatusCode status, string motivo)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(status, corpo);
        corpo.Should().Contain($"uniplus.autorizacao.{motivo}");
    }

    private static async Task<(Guid Id, JsonElement Corpo)> AbrirAsync(HttpClient conduz)
    {
        using HttpResponseMessage resposta = await EnviarAsync(conduz, HttpMethod.Post, RotaDasRevisoes);
        JsonElement corpo = await CorpoAsync(resposta, HttpStatusCode.Created);
        return (corpo.GetProperty("id").GetGuid(), corpo);
    }
}
