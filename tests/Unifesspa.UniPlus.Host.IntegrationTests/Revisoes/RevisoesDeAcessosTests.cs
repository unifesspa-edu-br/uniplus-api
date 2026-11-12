namespace Unifesspa.UniPlus.Host.IntegrationTests.Revisoes;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Contracts;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// A revisão periódica dos acessos pelo host real (ADR-0152). Marlene é Representante do controlador e conduz a revisão; Diogo,
/// Isabella e Betina são gestores de acessos; Leandro tem o acesso sensível a rever.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class RevisoesDeAcessosTests : PedidosDeConcessaoTestBase
{
    private const string RotaDasRevisoes = "/api/autorizacao/revisoes";
    private const string Motivo = "O atendimento do edital terminou.";
    private const string Representante = "representante-do-controlador";

    public RevisoesDeAcessosTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A revisão traz só os acessos pessoais ou sensíveis e manda os dos Representantes aos gestores")]
    public async Task Abrir_TrazSoPessoaisOuSensiveis()
    {
        await ResetarAsync();
        Guid unidade = await SemearUnidadeAsync();
        await SemearConcessaoAsync(Leandro, await SemearPerfilSensivelAsync(), EscopoDaConcessao.DaUnidade(unidade));
        await SemearConcessaoAsync(Betina, await SemearPerfilInternoAsync(), EscopoDaConcessao.DaUnidade(unidade));
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);
        await ClienteComPermissoesAsync(Diogo, AprovarFuncional);

        (_, JsonElement revisao) = await AbrirAsync(marlene);

        ItensDe(revisao, Betina).Should().BeEmpty("o perfil só interno não entra");
        (ItemDe(revisao, Leandro).GetProperty("decide").GetString(), ItemDe(revisao, Leandro).GetProperty("situacao").GetString()).Should().Be((Representante, "pendente"));
        ItemDe(revisao, Marlene).GetProperty("decide").GetString().Should().Be("gestor-de-acessos");
        ItemDe(revisao, Diogo).GetProperty("decide").GetString().Should().Be(Representante);
        revisao.GetProperty("situacao").GetString().Should().Be("em_andamento");
    }

    [Fact(DisplayName = "Só uma revisão fica em andamento, e só o Representante do controlador a abre")]
    public async Task Abrir_UmaPorVezESoPeloRepresentante()
    {
        await ResetarAsync();
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        await AbrirAsync(marlene);

        await ConferirErroAsync(await EnviarAsync(marlene, HttpMethod.Post, RotaDasRevisoes), HttpStatusCode.Conflict, "revisao.ja_ha_em_andamento");
        (await Cliente(Leandro).GetAsync(new Uri(RotaDasRevisoes, UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await EnviarAsync(diogo, HttpMethod.Post, RotaDasRevisoes)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "Revogar na revisão exige o motivo e encerra o acesso na próxima requisição")]
    public async Task Revogar_EncerraOAcesso()
    {
        (Guid revisaoId, JsonElement item, HttpClient marlene) = await RevisaoComTresGestoresAsync();
        using HttpClient diogo = Cliente(Diogo);
        string rota = $"{RotaDasRevisoes}/{revisaoId}/itens/{item.GetProperty("id").GetGuid()}/revogacao";
        (await diogo.GetAsync(new Uri("/api/autorizacao/concessoes", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.OK);

        await ConferirErroAsync(await EnviarAsync(marlene, HttpMethod.Post, rota, new { motivo = " " }), HttpStatusCode.UnprocessableEntity, "concessao.motivo_invalido");
        (await EnviarAsync(marlene, HttpMethod.Post, rota, new { motivo = Motivo })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await diogo.GetAsync(new Uri("/api/autorizacao/concessoes", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        JsonElement depois = ItemDe(await LerAsync(marlene, $"{RotaDasRevisoes}/{revisaoId}"), Diogo);
        (depois.GetProperty("situacao").GetString(), depois.GetProperty("decididoPor").GetProperty("sub").GetString()).Should().Be(("revogada", Marlene.Sub.ToString()));
    }

    [Fact(DisplayName = "A revogação na revisão entra na trilha da concessão, e o item decidido não se decide de novo")]
    public async Task Revogar_RegistraNaTrilha()
    {
        (Guid revisaoId, JsonElement item, HttpClient marlene) = await RevisaoComTresGestoresAsync();
        string rota = $"{RotaDasRevisoes}/{revisaoId}/itens/{item.GetProperty("id").GetGuid()}";

        (await EnviarAsync(marlene, HttpMethod.Post, $"{rota}/revogacao", new { motivo = Motivo })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        JsonElement trilha = await LerAsync(Cliente(Isabella), $"/api/autorizacao/concessoes/{item.GetProperty("concessaoId").GetGuid()}/trilha");
        trilha.EnumerateArray().Select(e => e.GetProperty("tipo").GetString()!).Should().Contain(["revogada", "revisao_revogada"]);
        await ConferirErroAsync(await EnviarAsync(marlene, HttpMethod.Post, $"{rota}/confirmacao"), HttpStatusCode.Conflict, "revisao.item_ja_decidido");
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

    [Fact(DisplayName = "O item do Representante só é decidido pelos gestores, e a confirmação entra na trilha sem encerrar o acesso")]
    public async Task ItemDoRepresentante_SoOsGestoresDecidem()
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

    [Fact(DisplayName = "O gestor de acessos não decide o item que cabe ao Representante do controlador, e a concessão segue ativa")]
    public async Task ItemComum_GestorNaoDecide()
    {
        await ResetarAsync();
        await SemearConcessaoAsync(Leandro, await SemearPerfilSensivelAsync(), EscopoDaConcessao.DaUnidade(await SemearUnidadeAsync()));
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        (Guid revisaoId, JsonElement revisao) = await AbrirAsync(marlene);
        string rotaDoItem = $"{RotaDasRevisoes}/{revisaoId}/itens/{ItemDe(revisao, Leandro).GetProperty("id").GetGuid()}";

        await ConferirErroAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{rotaDoItem}/confirmacao"), HttpStatusCode.UnprocessableEntity, "revisao.decisor_nao_decide_o_item");
        await ConferirErroAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{rotaDoItem}/revogacao", new { motivo = Motivo }), HttpStatusCode.UnprocessableEntity, "revisao.decisor_nao_decide_o_item");

        JsonElement item = ItemDe(await LerAsync(marlene, $"{RotaDasRevisoes}/{revisaoId}"), Leandro);
        item.GetProperty("situacao").GetString().Should().Be("pendente");
        (await LerAsync(diogo, $"/api/autorizacao/concessoes/{item.GetProperty("concessaoId").GetGuid()}/trilha")).EnumerateArray()
            .Select(e => e.GetProperty("tipo").GetString()).Should().NotContain("revogada");
    }

    [Fact(DisplayName = "Abrir a revisão sem conduzir registra a negativa de conduzir, e não a permissão de rever")]
    public async Task Abrir_SemConduzir_RegistraSoANegativaDeConduzir()
    {
        await ResetarAsync();
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        RegistroDeDecisoesDeTeste registro = Fixture.Factory.Services.GetRequiredService<RegistroDeDecisoesDeTeste>();
        int antes = registro.Registrados.Count;

        (await EnviarAsync(diogo, HttpMethod.Post, RotaDasRevisoes)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        registro.Registrados.Skip(antes).Where(r => r.AtorSubject == Diogo.Sub.ToString())
            .Select(static r => (r.Permissao, r.Resultado))
            .Should().BeEquivalentTo([("autorizacao:revisoes:conduzir", ResultadoDoAcesso.Negado)]);
    }

    [Fact(DisplayName = "Decidir o item dos gestores sem rever registra a negativa de rever, e não a permissão de conduzir")]
    public async Task DecidirItem_SemAGovernanciaDoItem_RegistraAPermissaoQueGovernaOItem()
    {
        await ResetarAsync();
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        (Guid revisaoId, JsonElement revisao) = await AbrirAsync(marlene);
        string rota = $"{RotaDasRevisoes}/{revisaoId}/itens/{ItemDe(revisao, Marlene).GetProperty("id").GetGuid()}/confirmacao";
        RegistroDeDecisoesDeTeste registro = Fixture.Factory.Services.GetRequiredService<RegistroDeDecisoesDeTeste>();
        int antes = registro.Registrados.Count;

        await ConferirErroAsync(await EnviarAsync(marlene, HttpMethod.Post, rota), HttpStatusCode.UnprocessableEntity, "revisao.decisor_nao_decide_o_item");

        registro.Registrados.Skip(antes).Where(r => r.AtorSubject == Marlene.Sub.ToString())
            .Select(r => (r.Permissao, r.Resultado, r.ResourceId))
            .Should().BeEquivalentTo([("autorizacao:revisoes:rever-protecao-de-dados", ResultadoDoAcesso.Negado, (Guid?)revisaoId)]);
    }

    [Fact(DisplayName = "O acesso comum de quem é Representante é revisto pelos gestores, e o próprio Representante não o decide")]
    public async Task AcessoComumDoRepresentante_VaiParaOsGestores()
    {
        await ResetarAsync();
        await ConcessoesDeTeste.ConcederAsync(
            Fixture.Factory.Services, Marlene.Sub.ToString(), await SemearPerfilSensivelAsync(), EscopoDaConcessao.DaUnidade(await SemearUnidadeAsync()));
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
            await EnviarAsync(marlene, HttpMethod.Post, $"{RotaDasRevisoes}/{Guid.NewGuid()}/itens/{Guid.NewGuid()}/confirmacao"), HttpStatusCode.NotFound, "revisao.nao_encontrada");
        await ConferirErroAsync(
            await EnviarAsync(marlene, HttpMethod.Post, $"{RotaDasRevisoes}/{revisaoId}/itens/{Guid.NewGuid()}/revogacao", new { motivo = Motivo }), HttpStatusCode.NotFound, "revisao.item_nao_encontrado");
        await ConferirErroAsync(
            await marlene.GetAsync(new Uri($"{RotaDasRevisoes}/{Guid.NewGuid()}", UriKind.Relative)), HttpStatusCode.NotFound, "revisao.nao_encontrada");
    }

    [Fact(DisplayName = "Concessão encerrada fora da revisão deixa de ser pendente e não aceita decisão")]
    public async Task ConcessaoEncerradaForaDaRevisao_NaoPedeDecisao()
    {
        await ResetarAsync();
        await ConcessoesDeTeste.ConcederAsync(
            Fixture.Factory.Services, Leandro.Sub.ToString(), await SemearPerfilSensivelAsync(), EscopoDaConcessao.DaUnidade(await SemearUnidadeAsync()));
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);
        (Guid revisaoId, JsonElement revisao) = await AbrirAsync(marlene);
        JsonElement item = ItemDe(revisao, Leandro);

        await RevogarForaDaRevisaoAsync(item.GetProperty("concessaoId").GetGuid());

        JsonElement depois = await LerAsync(marlene, $"{RotaDasRevisoes}/{revisaoId}");
        ItemDe(depois, Leandro).GetProperty("situacao").GetString().Should().Be("concessao_encerrada");
        depois.GetProperty("pendentes").GetInt32().Should().Be(revisao.GetProperty("pendentes").GetInt32() - 1);
        await ConferirErroAsync(
            await EnviarAsync(marlene, HttpMethod.Post, $"{RotaDasRevisoes}/{revisaoId}/itens/{item.GetProperty("id").GetGuid()}/confirmacao"), HttpStatusCode.Conflict, "revisao.concessao_nao_ativa");
    }

    // A revisão aberta por Marlene com Diogo, Isabella e Betina gestores, e o item da concessão de gestor de Diogo.
    private async Task<(Guid RevisaoId, JsonElement ItemDeDiogo, HttpClient Marlene)> RevisaoComTresGestoresAsync()
    {
        await ResetarAsync();
        HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);
        foreach (Persona gestor in new[] { Diogo, Isabella, Betina })
        {
            await ClienteComPermissoesAsync(gestor, AprovarFuncional);
        }

        (Guid revisaoId, JsonElement revisao) = await AbrirAsync(marlene);
        return (revisaoId, ItemDe(revisao, Diogo), marlene);
    }

    private async Task RevogarForaDaRevisaoAsync(Guid concessaoId)
    {
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        await ClienteComPermissoesAsync(Betina, AprovarFuncional);
        (await EnviarAsync(diogo, HttpMethod.Post, $"/api/autorizacao/concessoes/{concessaoId}/revogacao", new { motivo = Motivo, confirmacaoEmail = ConcessoesDeTeste.EmailDeTeste }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
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
