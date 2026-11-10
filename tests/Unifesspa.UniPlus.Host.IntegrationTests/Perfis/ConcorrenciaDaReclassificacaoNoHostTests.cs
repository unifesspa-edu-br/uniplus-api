namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// A reclassificação com operações concorrentes de verdade: a transação do teste segura a trava do perfil, o banco
/// registra cada operação parada nela e a liberação decide quem segue primeiro. Em nenhuma ordem a permissão escapa da
/// suspensão, e a decisão encontra o que a operação anterior confirmou.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class ConcorrenciaDaReclassificacaoNoHostTests : ReclassificacaoNoHostTestBase
{
    public ConcorrenciaDaReclassificacaoNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Theory(DisplayName = "A conciliação e a criação de concessão, em qualquer ordem, deixam as duas concessões sem a permissão reclassificada")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Conciliacao_ContraCriacaoDeConcessao(bool conciliacaoPrimeiro)
    {
        Cenario perfil = await CenarioAsync([Consultar, Sensivel], Leandro);
        await RebaixarRetratoAsync(perfil.Perfil, 1, Sensivel, "interno");
        await ImplantarAsync(Sensivel, ClassificacaoDeProtecao.Interno);
        Func<Task> conciliacao = ImplantarRealAsync;
        Func<Task> concessao = () => ConcederDepoisAsync(perfil, Betina);

        await EmFilaAsync(perfil.Perfil, conciliacaoPrimeiro ? [conciliacao, concessao] : [concessao, conciliacao]);

        (await PermissoesAsync(Leandro, perfil.Perfil)).Should().Equal(Consultar);
        (await PermissoesAsync(Betina, perfil.Perfil)).Should().Equal(Consultar);
    }

    [Fact(DisplayName = "A decisão de acesso com a conciliação em curso não vê o catálogo pela metade: recusa antes e decide depois do commit")]
    public async Task Conciliacao_ContraDecisaoDeAcesso()
    {
        Cenario perfil = await CenarioAsync([Consultar, Sensivel], Leandro);
        await ImplantarAsync(Sensivel, ClassificacaoDeProtecao.Interno);

        await EnquantoEmEsperaAsync(
            perfil.Perfil,
            ImplantarRealAsync,
            () => FluentActions.Awaiting(() => PermissoesAsync(Leandro, perfil.Perfil)).Should().ThrowAsync<CatalogoDesatualizadoException>());

        (await PermissoesAsync(Leandro, perfil.Perfil)).Should().Equal(Consultar);
    }

    [Fact(DisplayName = "A aprovação final antes do commit da conciliação recusa pelo catálogo; depois dele, põe a versão em vigor com a reclassificação")]
    public async Task AprovacaoFinal_ContraAConciliacao()
    {
        using HttpClient gestor = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(gestor, Consultar);
        Envio nova = await PendenteVerificadaAsync(gestor, vigente.PerfilId, Consultar, Sensivel);
        await RebaixarRetratoAsync(vigente.PerfilId, 2, Sensivel, "interno");
        using HttpClient gestao = await GestaoAsync();
        await AprovarAsync(gestao, nova, EtapaGestao);
        await ImplantarAsync(Sensivel, ClassificacaoDeProtecao.Interno);
        using HttpClient protecao = await ProtecaoAsync();
        HttpStatusCode? antes = null;

        await EnquantoEmEsperaAsync(
            vigente.PerfilId,
            ImplantarRealAsync,
            async () => antes = (await LerRespostaAsync(AprovarBrutoAsync(protecao, nova, EtapaProtecao))).Status);

        antes.Should().Be(HttpStatusCode.ServiceUnavailable);
        await AprovarAsync(protecao, nova, EtapaProtecao);
        await ConcederDepoisAsync(new Cenario(vigente.PerfilId, await SemearUnidadeAsync(), new Dictionary<Persona, Guid>()), Betina);
        (await ReclassificacoesAsync(vigente.PerfilId)).Should().Be(1);
        (await FilaDaProtecaoAsync()).Should().Contain((await ReclassificacaoAsync(vigente.PerfilId, Sensivel)).ToString());
    }

    [Fact(DisplayName = "Duas decisões simultâneas da mesma etapa: uma grava e a outra recebe conflito")]
    public async Task DuasDecisoes_UmaGravaEAOutraRecebeConflito()
    {
        Cenario perfil = await CenarioAsync([Sensivel], Leandro);
        await SubirAClasseAsync();
        Guid reclassificacao = await ReclassificacaoAsync(perfil.Perfil, Sensivel);
        using HttpClient primeira = await PrepararDecisorAsync(Marlene, EtapaProtecao, perfil.Perfil);
        using HttpClient segunda = await PrepararDecisorAsync(Isabella, EtapaProtecao, perfil.Perfil);
        (HttpStatusCode Status, string Corpo) um = default;
        (HttpStatusCode Status, string Corpo) outro = default;

        await EmFilaAsync(
            perfil.Perfil,
            async () => um = await LerRespostaAsync(DecidirComoAsync(primeira, reclassificacao, perfil.De(Leandro))),
            async () => outro = await LerRespostaAsync(DecidirComoAsync(segunda, reclassificacao, perfil.De(Leandro))));

        um.Status.Should().Be(HttpStatusCode.OK, um.Corpo);
        outro.Status.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity);
        (await PermissoesAsync(Leandro, perfil.Perfil)).Should().Equal(Sensivel);
    }

    [Fact(DisplayName = "Revogação confirmada antes da decisão: a decisão relê depois da trava, recusa a concessão e não grava liberação nem base")]
    public async Task Revogacao_ConfirmadaAntes_RecusaADecisao()
    {
        Cenario perfil = await CenarioAsync([Sensivel], Leandro);
        await SubirAClasseAsync();
        Guid reclassificacao = await ReclassificacaoAsync(perfil.Perfil, Sensivel);
        using HttpClient decisor = await PrepararDecisorAsync(Marlene, EtapaProtecao, perfil.Perfil);
        using HttpClient administrador = await ClienteComPermissoesAsync(Diogo, Administrar);
        (HttpStatusCode Status, string Corpo) revogacao = default;
        (HttpStatusCode Status, string Corpo) decisao = default;

        await EmFilaSobAConcessaoAsync(
            perfil.De(Leandro),
            async () => revogacao = await LerRespostaAsync(RevogarAsync(administrador, perfil.De(Leandro))),
            async () => decisao = await LerRespostaAsync(DecidirComoAsync(decisor, reclassificacao, perfil.De(Leandro))));

        revogacao.Status.Should().Be(HttpStatusCode.NoContent, revogacao.Corpo);
        decisao.Status.Should().Be(HttpStatusCode.UnprocessableEntity, decisao.Corpo);
        decisao.Corpo.Should().Contain("uniplus.autorizacao.reclassificacao.concessao_fora_da_reclassificacao");
        (await LiberacoesAsync(reclassificacao)).Should().Be(0);
        (await BasesDaOrigemAsync(perfil.Perfil, "liberacao")).Should().Be(0);
        (await PermissoesAsync(Leandro, perfil.Perfil)).Should().BeEmpty();
    }

    [Fact(DisplayName = "Decisão confirmada antes da revogação: a decisão fica gravada com a base e a concessão é revogada depois")]
    public async Task Decisao_ConfirmadaAntes_FicaGravadaERevogacaoSegue()
    {
        Cenario perfil = await CenarioAsync([Sensivel], Leandro);
        await SubirAClasseAsync();
        Guid reclassificacao = await ReclassificacaoAsync(perfil.Perfil, Sensivel);
        using HttpClient decisor = await PrepararDecisorAsync(Marlene, EtapaProtecao, perfil.Perfil);
        using HttpClient administrador = await ClienteComPermissoesAsync(Diogo, Administrar);
        (HttpStatusCode Status, string Corpo) decisao = default;
        (HttpStatusCode Status, string Corpo) revogacao = default;

        await EmFilaSobAConcessaoAsync(
            perfil.De(Leandro),
            async () => decisao = await LerRespostaAsync(DecidirComoAsync(decisor, reclassificacao, perfil.De(Leandro))),
            async () => revogacao = await LerRespostaAsync(RevogarAsync(administrador, perfil.De(Leandro))));

        decisao.Status.Should().Be(HttpStatusCode.OK, decisao.Corpo);
        revogacao.Status.Should().Be(HttpStatusCode.NoContent, revogacao.Corpo);
        (await LiberacoesAsync(reclassificacao)).Should().Be(1);
        (await OrigemDaBaseAsync(perfil.De(Leandro), Sensivel)).Should().Be("liberacao");
        (await PermissoesAsync(Leandro, perfil.Perfil)).Should().BeEmpty();
    }

    private static Task<HttpResponseMessage> DecidirComoAsync(HttpClient cliente, Guid reclassificacao, Guid concessao) =>
        EnviarAsync(
            cliente,
            HttpMethod.Post,
            $"{RotaReclassificacoes}/{reclassificacao}/decisoes",
            new { etapa = EtapaProtecao, resultado = "liberada", concessoes = new[] { concessao }, todas = false });

    private static Task<HttpResponseMessage> RevogarAsync(HttpClient administrador, Guid concessao) =>
        EnviarAsync(
            administrador,
            HttpMethod.Post,
            $"/api/autorizacao/concessoes/{concessao}/revogacao",
            new { motivo = "A pessoa deixou a função.", confirmacaoEmail = Leandro.Email });
}
