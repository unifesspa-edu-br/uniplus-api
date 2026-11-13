namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

using Resposta = (System.Net.HttpStatusCode Status, string Corpo);

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
        Envio nova = await NovaVersaoAprovadaPelaGestaoAsync();
        using HttpClient protecao = await ProtecaoAsync();
        HttpStatusCode? antes = null;

        await EnquantoEmEsperaAsync(
            nova.PerfilId,
            ImplantarRealAsync,
            async () => antes = (await LerRespostaAsync(AprovarBrutoAsync(protecao, nova, EtapaProtecao))).Status);

        antes.Should().Be(HttpStatusCode.ServiceUnavailable);
        await AprovarAsync(protecao, nova, EtapaProtecao);
        await ConcederDepoisAsync(new Cenario(nova.PerfilId, await SemearUnidadeAsync(), new Dictionary<Persona, Guid>()), Betina);
        (await ReclassificacoesAsync(nova.PerfilId)).Should().Be(1);
        (await FilaDaProtecaoAsync()).Should().Contain((await ReclassificacaoAsync(nova.PerfilId, Sensivel)).ToString());
    }

    [Fact(DisplayName = "Duas decisões simultâneas da mesma etapa: uma grava e a outra recebe conflito")]
    public async Task DuasDecisoes_UmaGravaEAOutraRecebeConflito()
    {
        (Cenario perfil, Guid reclassificacao) = await ReclassificacaoAbertaAsync();
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
        (Cenario perfil, Guid reclassificacao) = await ReclassificacaoAbertaAsync();

        (Resposta revogacao, Resposta decisao) = await RevogacaoEDecisaoEmFilaAsync(perfil, reclassificacao, revogacaoPrimeiro: true);

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
        (Cenario perfil, Guid reclassificacao) = await ReclassificacaoAbertaAsync();

        (Resposta revogacao, Resposta decisao) = await RevogacaoEDecisaoEmFilaAsync(perfil, reclassificacao, revogacaoPrimeiro: false);

        decisao.Status.Should().Be(HttpStatusCode.OK, decisao.Corpo);
        revogacao.Status.Should().Be(HttpStatusCode.NoContent, revogacao.Corpo);
        (await LiberacoesAsync(reclassificacao)).Should().Be(1);
        (await OrigemDaBaseAsync(perfil.De(Leandro), Sensivel)).Should().Be("liberacao");
        (await PermissoesAsync(Leandro, perfil.Perfil)).Should().BeEmpty();
    }

    /// <summary>A versão 2 acrescenta a sensível, com o retrato dela rebaixado, aprovada pela gestão e com a reclassificação implantada.</summary>
    private async Task<Envio> NovaVersaoAprovadaPelaGestaoAsync()
    {
        using HttpClient gestor = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(gestor, Consultar);
        Envio nova = await PendenteVerificadaAsync(gestor, vigente.PerfilId, Consultar, Sensivel);
        await RebaixarRetratoAsync(vigente.PerfilId, 2, Sensivel, "interno");
        using HttpClient gestao = await GestaoAsync();
        await AprovarAsync(gestao, nova, EtapaGestao);
        await ImplantarAsync(Sensivel, ClassificacaoDeProtecao.Interno);

        return nova;
    }

    /// <summary>O perfil com a sensível concedida a Leandro, depois que a classe dela subiu e abriu a reclassificação.</summary>
    private async Task<(Cenario Perfil, Guid Reclassificacao)> ReclassificacaoAbertaAsync()
    {
        Cenario perfil = await CenarioAsync([Sensivel], Leandro);
        await SubirAClasseAsync();

        return (perfil, await ReclassificacaoAsync(perfil.Perfil, Sensivel));
    }

    /// <summary>
    /// A revogação da concessão de Leandro pelo endpoint e a decisão da proteção de dados que a libera param na trava da
    /// concessão, na ordem pedida, e seguem quando o teste a solta.
    /// </summary>
    private async Task<(Resposta Revogacao, Resposta Decisao)> RevogacaoEDecisaoEmFilaAsync(
        Cenario perfil, Guid reclassificacao, bool revogacaoPrimeiro)
    {
        using HttpClient decisor = await PrepararDecisorAsync(Marlene, EtapaProtecao, perfil.Perfil);
        using HttpClient administrador = await ClienteComPermissoesAsync(Diogo, Administrar);
        Resposta revogacao = default;
        Resposta decisao = default;
        Func<Task> revogar = async () => revogacao = await LerRespostaAsync(RevogarAsync(administrador, perfil.De(Leandro), MotivoDaRevogacao, Leandro.Email));
        Func<Task> decidir = async () => decisao = await LerRespostaAsync(DecidirComoAsync(decisor, reclassificacao, perfil.De(Leandro)));

        await EmFilaSobAConcessaoAsync(perfil.De(Leandro), revogacaoPrimeiro ? [revogar, decidir] : [decidir, revogar]);

        return (revogacao, decisao);
    }

    private static Task<HttpResponseMessage> DecidirComoAsync(HttpClient cliente, Guid reclassificacao, Guid concessao) =>
        EnviarAsync(
            cliente,
            HttpMethod.Post,
            $"{RotaReclassificacoes}/{reclassificacao}/decisoes",
            new { etapa = EtapaProtecao, resultado = "liberada", concessoes = new[] { concessao }, todas = false });
}
