namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// A revisão das concessões e o envio pelo host real: a abertura, as decisões da gestão de acessos e da proteção
/// de dados, as recusas e a leitura.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class RevisaoDasConcessoesNoHostTests : RevisaoDasConcessoesNoHostTestBase
{
    public RevisaoDasConcessoesNoHostTests(MonolitoDaInstalacaoFixture fixture, HostDaRevisaoDasConcessoes hostDaClasse)
        : base(fixture, hostDaClasse)
    {
    }

    [Fact(DisplayName = "A versão que acrescenta permissão abre a revisão de 72 horas com cada concessão aguardando o envio")]
    public async Task VersaoComAcrescimo_AbreARevisao()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro, Betina);
        using HttpClient gestao = await GestaoAsync();

        JsonElement revisao = await RevisaoAsync(gestao, cenario.PerfilId);

        revisao.GetProperty("situacao").GetString().Should().Be("aberta");
        revisao.GetProperty("decisoesEnviadas").GetBoolean().Should().BeFalse();
        (revisao.GetProperty("terminaEm").GetDateTimeOffset() - revisao.GetProperty("abertaEm").GetDateTimeOffset()).Should().Be(TimeSpan.FromHours(72));
        revisao.GetProperty("itens").GetArrayLength().Should().Be(2);
        cenario.Concessoes.Values.Should().OnlyContain(id => SituacaoDe(revisao, id) == "aguardando_decisoes");
        (await PerfilAsync(gestao, cenario.PerfilId)).GetProperty("revisaoDasConcessoes").GetProperty("situacao").GetString().Should().Be("aberta");
    }

    [Fact(DisplayName = "O envio que migra tudo leva a concessão com acréscimo sensível à proteção de dados, que conclui a revisão")]
    public async Task Envio_MigraETerminaNaProtecaoDeDados()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        Guid concessao = cenario.Concessoes[Leandro];
        using HttpClient gestao = await GestaoAsync();

        using HttpResponseMessage envio = await EnviarDecisoesDaGestaoAsync(gestao, cenario.PerfilId, [Decisao(concessao, "MIGRAR")]);

        SituacaoDe(await CorpoAsync(envio, HttpStatusCode.OK), concessao).Should().Be("AGUARDANDO_PROTECAO_DE_DADOS");
        using HttpClient protecao = await ProtecaoAsync();
        using HttpResponseMessage etapa = await EnviarAsync(
            protecao, HttpMethod.Post, RotaDaRevisao(cenario.PerfilId, "/protecao-de-dados"), new { decisoes = new[] { Decisao(concessao, "MIGRAR") } });
        JsonElement revisao = await CorpoAsync(etapa, HttpStatusCode.OK);
        SituacaoDe(revisao, concessao).Should().Be("MIGRADA");
        revisao.GetProperty("situacao").GetString().Should().Be("encerrada");
        revisao.GetProperty("forma").GetString().Should().Be("CONCLUIDA");
    }

    [Fact(DisplayName = "O envio que recusa exige a confirmação da exclusão e, confirmado, revoga a concessão")]
    public async Task Envio_RecusaExigeConfirmacao()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        Guid concessao = cenario.Concessoes[Leandro];
        using HttpClient gestao = await GestaoAsync();

        using HttpResponseMessage semConfirmacao = await EnviarDecisoesDaGestaoAsync(gestao, cenario.PerfilId, [Decisao(concessao, "RECUSAR")]);
        using HttpResponseMessage confirmado = await EnviarDecisoesDaGestaoAsync(gestao, cenario.PerfilId, [Decisao(concessao, "RECUSAR")], confirmacaoDeExclusao: true);

        await ConferirRecusaDaMigracaoAsync(semConfirmacao, HttpStatusCode.UnprocessableEntity, "exclusao_nao_confirmada");
        SituacaoDe(await CorpoAsync(confirmado, HttpStatusCode.OK), concessao).Should().Be("RECUSADA");
        (await TemConcessaoAtivaAsync(Leandro, cenario.PerfilId)).Should().BeFalse();
    }

    [Fact(DisplayName = "O envio que não decide todas as concessões é recusado, e o segundo envio também")]
    public async Task Envio_IncompletoEDuplicado_Recusa()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro, Betina);
        using HttpClient gestao = await GestaoAsync();

        using HttpResponseMessage incompleto = await EnviarDecisoesDaGestaoAsync(gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "MIGRAR")]);
        using HttpResponseMessage completo = await EnviarDecisoesDaGestaoAsync(
            gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "MIGRAR"), Decisao(cenario.Concessoes[Betina], "MIGRAR")]);
        using HttpResponseMessage segundo = await EnviarDecisoesDaGestaoAsync(
            gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "MIGRAR"), Decisao(cenario.Concessoes[Betina], "MIGRAR")]);

        await ConferirRecusaDaMigracaoAsync(incompleto, HttpStatusCode.UnprocessableEntity, "decisoes_incompletas");
        SituacaoDe(await CorpoAsync(completo, HttpStatusCode.OK), cenario.Concessoes[Betina]).Should().Be("AGUARDANDO_PROTECAO_DE_DADOS");
        await ConferirRecusaDaMigracaoAsync(segundo, HttpStatusCode.Conflict, "decisoes_ja_enviadas");
    }

    [Fact(DisplayName = "A concessão de quem envia o envio fica retida e é decidida por outra pessoa da gestão")]
    public async Task Retida_DecididaPorOutraPessoa()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Isabella, Leandro);
        Guid propria = cenario.Concessoes[Isabella];
        using HttpClient isabella = await GestaoAsync();
        using HttpResponseMessage envio = await EnviarDecisoesDaGestaoAsync(isabella, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "MIGRAR")]);
        SituacaoDe(await CorpoAsync(envio, HttpStatusCode.OK), propria).Should().Be("RETIDA");
        using HttpClient diogo = await GestorAsync();

        using HttpResponseMessage propriaDecisao = await EnviarAsync(
            isabella, HttpMethod.Post, RotaDaRevisao(cenario.PerfilId, $"/retidas/{propria}"), new { decisao = "MIGRAR" });
        using HttpResponseMessage decidida = await EnviarAsync(
            diogo, HttpMethod.Post, RotaDaRevisao(cenario.PerfilId, $"/retidas/{propria}"), new { decisao = "MIGRAR" });

        await ConferirRecusaDaMigracaoAsync(propriaDecisao, HttpStatusCode.UnprocessableEntity, "decisao_da_propria_concessao");
        SituacaoDe(await CorpoAsync(decidida, HttpStatusCode.OK), propria).Should().Be("AGUARDANDO_PROTECAO_DE_DADOS");
    }

    [Fact(DisplayName = "Com a revisão aberta, a versão nova do perfil é recusada")]
    public async Task ComRevisaoAberta_VersaoNovaEhRecusada()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        using HttpClient gestor = await GestorAsync();

        using HttpResponseMessage resposta = await EnviarAsync(
            gestor, HttpMethod.Post, $"{RotaPerfis}/{cenario.PerfilId}/versoes", Dados(Nome(), Consultar, Sensivel));

        await ConferirRecusaDoPerfilAsync(resposta, HttpStatusCode.UnprocessableEntity, "revisao_das_concessoes_aberta");
    }

    [Fact(DisplayName = "Quem não aprova nem mantém perfis não lê a revisão")]
    public async Task SemPermissao_NaoLeARevisao()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        using HttpClient semPermissao = await ClienteComPermissoesAsync(Betina);

        using HttpResponseMessage resposta = await semPermissao.GetAsync(new Uri(RotaDaRevisao(cenario.PerfilId), UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden, await resposta.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "O perfil que nunca teve revisão responde 404")]
    public async Task SemRevisao_Responde404()
    {
        using HttpClient gestor = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(gestor, Consultar);
        using HttpClient gestao = await GestaoAsync();

        using HttpResponseMessage resposta = await gestao.GetAsync(new Uri(RotaDaRevisao(vigente.PerfilId), UriKind.Relative));

        await ConferirRecusaDaMigracaoAsync(resposta, HttpStatusCode.NotFound, "revisao_nao_encontrada");
    }

    [Fact(DisplayName = "O encerramento manual exige a confirmação e deixa quem não migrou sem o acesso")]
    public async Task EncerramentoManual_ExigeConfirmacao()
    {
        RevisaoAberta cenario = await AbrirRevisaoAsync(Leandro);
        using HttpClient gestao = await GestaoAsync();
        string rota = RotaDaRevisao(cenario.PerfilId, "/encerramento");

        using HttpResponseMessage semConfirmacao = await EnviarAsync(gestao, HttpMethod.Post, rota, new { motivo = "Prazo acordado com a gestão.", confirmacaoDeExclusao = false });
        using HttpResponseMessage confirmado = await EnviarAsync(gestao, HttpMethod.Post, rota, new { motivo = "Prazo acordado com a gestão.", confirmacaoDeExclusao = true });

        await ConferirRecusaDaMigracaoAsync(semConfirmacao, HttpStatusCode.UnprocessableEntity, "exclusao_nao_confirmada");
        JsonElement revisao = await CorpoAsync(confirmado, HttpStatusCode.OK);
        revisao.GetProperty("forma").GetString().Should().Be("MANUAL");
        SituacaoDe(revisao, cenario.Concessoes[Leandro]).Should().Be("NAO_MIGRADA");
        (await TemConcessaoAtivaAsync(Leandro, cenario.PerfilId)).Should().BeFalse();
    }
}
