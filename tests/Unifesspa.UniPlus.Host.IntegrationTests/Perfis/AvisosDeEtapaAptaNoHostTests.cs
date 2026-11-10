namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

using Wolverine.Tracking;

/// <summary>
/// O aviso de que a versão pendente ficou apta, pela fila do host real: o veredicto do documento avisa a gestão, o
/// mesmo veredicto reentregue não avisa de novo, e a aprovação da gestão avisa a proteção de dados.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class AvisosDeEtapaAptaNoHostTests : AprovacaoDaVersaoNoHostTestBase
{
    private static readonly string Apta = TipoDeAviso.VersaoPendenteApta.Finalidade();

    public AvisosDeEtapaAptaNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "O documento verificado avisa a gestão, menos quem criou a versão, e o veredicto reentregue não avisa de novo")]
    public async Task DocumentoVerificado_AvisaAGestaoUmaVez()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(gestor, Consultar);
        Envio nova = await CriarVersaoAsync(gestor, vigente.PerfilId, Nome(), Consultar, Sensivel);
        await ConfirmarEnvioAsync(gestor, nova);

        ITrackedSession primeira = await EntregarPelaFilaAsync(nova.DocumentoId);
        ITrackedSession reentrega = await EntregarPelaFilaAsync(nova.DocumentoId);

        Destinatarios(primeira).Should().BeEquivalentTo([Isabella.Email]);
        Destinatarios(reentrega).Should().BeEmpty();
    }

    [Fact(DisplayName = "A aprovação da gestão avisa a proteção de dados, menos quem aprovou pela gestão")]
    public async Task AprovacaoDaGestao_AvisaAProtecaoDeDados()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(gestor, Consultar);
        Envio nova = await PendenteVerificadaAsync(gestor, vigente.PerfilId, Consultar, Sensivel);
        using HttpClient gestao = await GestaoAsync();
        using HttpClient protecao = await ProtecaoAsync();

        ITrackedSession sessao = await RastrearAsync(() => AprovarAsync(gestao, nova, EtapaGestao));

        Destinatarios(sessao).Should().BeEquivalentTo([Marlene.Email]);
    }

    private static string[] Destinatarios(ITrackedSession sessao) =>
        [.. sessao.Sent.MessagesOf<EnviarEmail>().Where(m => m.Finalidade == Apta).Select(m => m.Destinatario)];
}
