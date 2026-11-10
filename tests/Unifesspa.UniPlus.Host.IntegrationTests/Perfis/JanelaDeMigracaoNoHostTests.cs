namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// A janela de migração das concessões e o lote pelo host real: a abertura, as decisões da gestão de acessos e da proteção
/// de dados, as recusas e a leitura.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class JanelaDeMigracaoNoHostTests : JanelaDeMigracaoNoHostTestBase
{
    public JanelaDeMigracaoNoHostTests(MonolitoDaInstalacaoFixture fixture, HostDaJanelaDeMigracao hostDaClasse)
        : base(fixture, hostDaClasse)
    {
    }

    [Fact(DisplayName = "A versão que acrescenta permissão abre a janela de 72 horas com cada concessão aguardando o lote")]
    public async Task VersaoComAcrescimo_AbreAJanela()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro, Betina);
        using HttpClient gestao = await GestaoAsync();

        JsonElement janela = await JanelaAsync(gestao, cenario.PerfilId);

        janela.GetProperty("situacao").GetString().Should().Be("aberta");
        janela.GetProperty("loteEnviado").GetBoolean().Should().BeFalse();
        (janela.GetProperty("prazo").GetDateTimeOffset() - janela.GetProperty("abertaEm").GetDateTimeOffset()).Should().Be(TimeSpan.FromHours(72));
        janela.GetProperty("itens").GetArrayLength().Should().Be(2);
        cenario.Concessoes.Values.Should().OnlyContain(id => SituacaoDe(janela, id) == "aguardando_lote");
        (await PerfilAsync(gestao, cenario.PerfilId)).GetProperty("janela").GetProperty("situacao").GetString().Should().Be("aberta");
    }

    [Fact(DisplayName = "O lote que migra tudo leva a concessão com acréscimo sensível à proteção de dados, que conclui a janela")]
    public async Task Lote_MigraETerminaNaProtecaoDeDados()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        Guid concessao = cenario.Concessoes[Leandro];
        using HttpClient gestao = await GestaoAsync();

        using HttpResponseMessage lote = await EnviarLoteAsync(gestao, cenario.PerfilId, [Decisao(concessao, "migrar")]);

        SituacaoDe(await CorpoAsync(lote, HttpStatusCode.OK), concessao).Should().Be("aguardando_protecao_de_dados");
        using HttpClient protecao = await ProtecaoAsync();
        using HttpResponseMessage etapa = await EnviarAsync(
            protecao, HttpMethod.Post, RotaDaJanela(cenario.PerfilId, "/protecao-de-dados"), new { decisoes = new[] { Decisao(concessao, "migrar") } });
        JsonElement janela = await CorpoAsync(etapa, HttpStatusCode.OK);
        SituacaoDe(janela, concessao).Should().Be("migrada");
        janela.GetProperty("situacao").GetString().Should().Be("encerrada");
        janela.GetProperty("forma").GetString().Should().Be("concluida");
    }

    [Fact(DisplayName = "O lote que recusa exige a confirmação da exclusão e, confirmado, revoga a concessão")]
    public async Task Lote_RecusaExigeConfirmacao()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        Guid concessao = cenario.Concessoes[Leandro];
        using HttpClient gestao = await GestaoAsync();

        using HttpResponseMessage semConfirmacao = await EnviarLoteAsync(gestao, cenario.PerfilId, [Decisao(concessao, "recusar")]);
        using HttpResponseMessage confirmado = await EnviarLoteAsync(gestao, cenario.PerfilId, [Decisao(concessao, "recusar")], confirmacaoDeExclusao: true);

        await ConferirRecusaDaMigracaoAsync(semConfirmacao, HttpStatusCode.UnprocessableEntity, "exclusao_nao_confirmada");
        SituacaoDe(await CorpoAsync(confirmado, HttpStatusCode.OK), concessao).Should().Be("recusada");
        (await TemConcessaoAtivaAsync(Leandro, cenario.PerfilId)).Should().BeFalse();
    }

    [Fact(DisplayName = "O lote que não decide todas as concessões é recusado, e o segundo lote também")]
    public async Task Lote_IncompletoEDuplicado_Recusa()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro, Betina);
        using HttpClient gestao = await GestaoAsync();

        using HttpResponseMessage incompleto = await EnviarLoteAsync(gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "migrar")]);
        using HttpResponseMessage completo = await EnviarLoteAsync(
            gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "migrar"), Decisao(cenario.Concessoes[Betina], "migrar")]);
        using HttpResponseMessage segundo = await EnviarLoteAsync(
            gestao, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "migrar"), Decisao(cenario.Concessoes[Betina], "migrar")]);

        await ConferirRecusaDaMigracaoAsync(incompleto, HttpStatusCode.UnprocessableEntity, "lote_incompleto");
        SituacaoDe(await CorpoAsync(completo, HttpStatusCode.OK), cenario.Concessoes[Betina]).Should().Be("aguardando_protecao_de_dados");
        await ConferirRecusaDaMigracaoAsync(segundo, HttpStatusCode.Conflict, "lote_ja_enviado");
    }

    [Fact(DisplayName = "A concessão de quem envia o lote fica retida e é decidida por outra pessoa da gestão")]
    public async Task Retida_DecididaPorOutraPessoa()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Isabella, Leandro);
        Guid propria = cenario.Concessoes[Isabella];
        using HttpClient isabella = await GestaoAsync();
        using HttpResponseMessage lote = await EnviarLoteAsync(isabella, cenario.PerfilId, [Decisao(cenario.Concessoes[Leandro], "migrar")]);
        SituacaoDe(await CorpoAsync(lote, HttpStatusCode.OK), propria).Should().Be("retida");
        using HttpClient diogo = await GestorAsync();

        using HttpResponseMessage propriaDecisao = await EnviarAsync(
            isabella, HttpMethod.Post, RotaDaJanela(cenario.PerfilId, $"/retidas/{propria}"), new { decisao = "migrar" });
        using HttpResponseMessage decidida = await EnviarAsync(
            diogo, HttpMethod.Post, RotaDaJanela(cenario.PerfilId, $"/retidas/{propria}"), new { decisao = "migrar" });

        await ConferirRecusaDaMigracaoAsync(propriaDecisao, HttpStatusCode.UnprocessableEntity, "decisao_da_propria_concessao");
        SituacaoDe(await CorpoAsync(decidida, HttpStatusCode.OK), propria).Should().Be("aguardando_protecao_de_dados");
    }

    [Fact(DisplayName = "Com a janela aberta, a versão nova do perfil é recusada")]
    public async Task ComJanelaAberta_VersaoNovaEhRecusada()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        using HttpClient gestor = await GestorAsync();

        using HttpResponseMessage resposta = await EnviarAsync(
            gestor, HttpMethod.Post, $"{RotaPerfis}/{cenario.PerfilId}/versoes", Dados(Nome(), Consultar, Sensivel));

        await ConferirRecusaDoPerfilAsync(resposta, HttpStatusCode.UnprocessableEntity, "janela_de_migracao_aberta");
    }

    [Fact(DisplayName = "Quem não aprova nem mantém perfis não lê a janela")]
    public async Task SemPermissao_NaoLeAJanela()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        using HttpClient semPermissao = await ClienteComPermissoesAsync(Betina);

        using HttpResponseMessage resposta = await semPermissao.GetAsync(new Uri(RotaDaJanela(cenario.PerfilId), UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden, await resposta.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "O perfil que nunca teve janela responde 404")]
    public async Task SemJanela_Responde404()
    {
        using HttpClient gestor = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(gestor, Consultar);
        using HttpClient gestao = await GestaoAsync();

        using HttpResponseMessage resposta = await gestao.GetAsync(new Uri(RotaDaJanela(vigente.PerfilId), UriKind.Relative));

        await ConferirRecusaDaMigracaoAsync(resposta, HttpStatusCode.NotFound, "janela_nao_encontrada");
    }

    [Fact(DisplayName = "O encerramento manual exige a confirmação e deixa quem não migrou sem o acesso")]
    public async Task EncerramentoManual_ExigeConfirmacao()
    {
        JanelaAberta cenario = await AbrirJanelaAsync(Leandro);
        using HttpClient gestao = await GestaoAsync();
        string rota = RotaDaJanela(cenario.PerfilId, "/encerramento");

        using HttpResponseMessage semConfirmacao = await EnviarAsync(gestao, HttpMethod.Post, rota, new { motivo = "Prazo acordado com a gestão.", confirmacaoDeExclusao = false });
        using HttpResponseMessage confirmado = await EnviarAsync(gestao, HttpMethod.Post, rota, new { motivo = "Prazo acordado com a gestão.", confirmacaoDeExclusao = true });

        await ConferirRecusaDaMigracaoAsync(semConfirmacao, HttpStatusCode.UnprocessableEntity, "exclusao_nao_confirmada");
        JsonElement janela = await CorpoAsync(confirmado, HttpStatusCode.OK);
        janela.GetProperty("forma").GetString().Should().Be("manual");
        SituacaoDe(janela, cenario.Concessoes[Leandro]).Should().Be("nao_migrada");
        (await TemConcessaoAtivaAsync(Leandro, cenario.PerfilId)).Should().BeFalse();
    }
}
