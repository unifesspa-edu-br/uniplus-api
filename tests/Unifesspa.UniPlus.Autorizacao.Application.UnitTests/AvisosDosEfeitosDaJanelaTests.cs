namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDeAvisos;

/// <summary>
/// Os avisos do que a janela de migração fez: a migração e a revogação ao beneficiário, o lote com recusas, as migrações
/// que aguardam o Representante do controlador e o encerramento. Só personas fictícias do portal.
/// </summary>
public sealed class AvisosDosEfeitosDaJanelaTests
{
    private const string Emissor = TitularesDaGovernanca.Emissor;
    private const string EmailDiogo = TitularesDaGovernanca.EmailDiogo;
    private const string EmailIsabella = TitularesDaGovernanca.EmailIsabella;
    private const string EmailMarlene = TitularesDaGovernanca.EmailMarlene;
    private const string EmailLeandro = "leandro-viana84@uniplus-test.local";
    private const string EmailBetina = "betina-dasneves81@uniplus-test.local";

    private static readonly Identidade Diogo = new(Emissor, "diogo.souza");
    private static readonly Identidade Betina = new(Emissor, "betina.neves");

    [Fact(DisplayName = "O lote avisa quem migrou, quem foi recusado e os grupos de decisão, menos quem o enviou")]
    public async Task Lote_AvisaBeneficiariosEGrupos()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deLeandro = cenario.Conceder(CenarioDeMigracao.Leandro, email: EmailLeandro);
        Concessao deBetina = cenario.Conceder(Betina, email: EmailBetina);
        cenario.Abrir();

        IReadOnlyList<EnviarEmail> enviados = await AvisosDoLoteAsync(
            cenario, true, (deLeandro, ResultadoDaMigracao.Migrar), (deBetina, ResultadoDaMigracao.Recusar));

        Destinatarios(enviados, TipoDeAviso.ConcessaoMigrada).Should().BeEquivalentTo([EmailLeandro]);
        Destinatarios(enviados, TipoDeAviso.ConcessaoRevogada).Should().BeEquivalentTo([EmailBetina]);
        Destinatarios(enviados, TipoDeAviso.LoteEnviadoComRecusas).Should().BeEquivalentTo([EmailIsabella, EmailMarlene]);
        enviados.Should().HaveCount(4);
    }

    [Fact(DisplayName = "As migrações que passam a aguardar o Representante do controlador avisam os Representantes e os gestores, menos quem enviou o lote")]
    public async Task Lote_QueAguardaAProtecao_AvisaRepresentantesEGestores()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        Concessao deLeandro = cenario.Conceder(CenarioDeMigracao.Leandro, email: EmailLeandro);
        cenario.Abrir();

        IReadOnlyList<EnviarEmail> enviados = await AvisosDoLoteAsync(cenario, false, (deLeandro, ResultadoDaMigracao.Migrar));

        Destinatarios(enviados, TipoDeAviso.MigracoesAguardandoProtecaoDeDados).Should().BeEquivalentTo([EmailIsabella, EmailMarlene]);
        enviados.Select(e => e.Destinatario).Should().NotContain(EmailLeandro);
    }

    [Fact(DisplayName = "O fim pelo prazo avisa cada beneficiário que perdeu o acesso e os grupos, sem lista de beneficiários")]
    public async Task Fim_PeloPrazo_AvisaBeneficiariosEGrupos()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        cenario.Conceder(CenarioDeMigracao.Leandro, email: EmailLeandro);
        cenario.Conceder(Betina, email: EmailBetina);
        cenario.Abrir();

        IReadOnlyList<EnviarEmail> enviados = await AvisosDoFimPeloPrazoAsync(cenario);

        Destinatarios(enviados, TipoDeAviso.ConcessaoRevogada).Should().BeEquivalentTo([EmailLeandro, EmailBetina]);
        Destinatarios(enviados, TipoDeAviso.VersaoEncerrada).Should().BeEquivalentTo([EmailDiogo, EmailIsabella, EmailMarlene]);
        enviados.Where(e => e.Finalidade == TipoDeAviso.VersaoEncerrada.Finalidade())
            .Should().OnlyContain(e => !e.CorpoTexto.Contains("Leandro", StringComparison.Ordinal) && !e.CorpoTexto.Contains(EmailLeandro, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "A revogação manual feita antes do fim não vira aviso de revogação pelo fim")]
    public async Task Fim_RevogacaoManualAnterior_NaoAvisa()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        cenario.Conceder(CenarioDeMigracao.Leandro, email: EmailLeandro);
        Concessao deBetina = cenario.Conceder(Betina, email: EmailBetina);
        JanelaDeMigracao janela = cenario.Abrir();
        deBetina.Revogar(new AtoDoAgente(Diogo, janela.Prazo.AddHours(3)), "Revogação manual.").IsSuccess.Should().BeTrue();
        DateTimeOffset depois = janela.Prazo.AddHours(10);
        EstadoDaJanelaAntesDaOperacao antes = EstadoDaJanelaAntesDaOperacao.De(janela, cenario.Contexto(depois));
        janela.Materializar(cenario.Contexto(depois), FormaDoEncerramento.Prazo);

        MudancasDaJanela mudancas = MudancasDaJanela.Entre(antes, janela, cenario.Contexto(depois));
        IReadOnlyList<EnviarEmail> enviados = await Efeitos().DoFimAsync(Operacao(cenario, depois), mudancas, null, CancellationToken.None);

        Destinatarios(enviados, TipoDeAviso.ConcessaoRevogada).Should().BeEquivalentTo([EmailLeandro]);
        mudancas.TotalRevogadas.Should().Be(1);
    }

    [Fact(DisplayName = "Sem encerramento na operação, o fim não avisa ninguém")]
    public async Task Fim_SemEncerramento_NaoAvisa()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        cenario.Conceder(CenarioDeMigracao.Leandro, email: EmailLeandro);
        JanelaDeMigracao janela = cenario.Abrir();
        DateTimeOffset agora = CenarioDeMigracao.Abertura.AddHours(1);
        EstadoDaJanelaAntesDaOperacao antes = EstadoDaJanelaAntesDaOperacao.De(janela, cenario.Contexto(agora));

        IReadOnlyList<EnviarEmail> enviados = await Efeitos().DoFimAsync(
            Operacao(cenario, agora), MudancasDaJanela.Entre(antes, janela, cenario.Contexto(agora)), Diogo, CancellationToken.None);

        enviados.Should().BeEmpty();
    }

    [Fact(DisplayName = "A versão anterior substituída sem janela avisa os grupos, menos quem praticou o ato")]
    public async Task VersaoSubstituida_AvisaOsGruposMenosOAutor()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        cenario.Abrir();

        IReadOnlyList<EnviarEmail> enviados = await Efeitos().DaVersaoSubstituidaAsync(
            cenario.Perfil, cenario.VersaoAntiga, FormaDoAviso.SemConcessaoAMigrar, Diogo, CancellationToken.None);

        Destinatarios(enviados, TipoDeAviso.VersaoEncerrada).Should().BeEquivalentTo([EmailIsabella, EmailMarlene]);
    }

    // O lote é enviado por Diogo uma hora depois da abertura.
    private static async Task<IReadOnlyList<EnviarEmail>> AvisosDoLoteAsync(
        CenarioDeMigracao cenario, bool confirmou, params (Concessao Concessao, ResultadoDaMigracao Decisao)[] itens)
    {
        JanelaDeMigracao janela = cenario.Janela!;
        DateTimeOffset agora = CenarioDeMigracao.Abertura.AddHours(1);
        EstadoDaJanelaAntesDaOperacao antes = EstadoDaJanelaAntesDaOperacao.De(janela, cenario.Contexto(agora));
        janela.EnviarLote(CenarioDeMigracao.Lote(confirmou, itens), CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(agora))
            .IsSuccess.Should().BeTrue();

        return await Efeitos().DaDecisaoAsync(
            Operacao(cenario, agora), MudancasDaJanela.Entre(antes, janela, cenario.Contexto(agora)), OperacaoNaJanela.Lote, Diogo, CancellationToken.None);
    }

    private static async Task<IReadOnlyList<EnviarEmail>> AvisosDoFimPeloPrazoAsync(CenarioDeMigracao cenario)
    {
        JanelaDeMigracao janela = cenario.Janela!;
        DateTimeOffset corte = janela.Prazo;
        EstadoDaJanelaAntesDaOperacao antes = EstadoDaJanelaAntesDaOperacao.De(janela, cenario.Contexto(corte));
        janela.ConferirEstado(cenario.Contexto(corte));

        return await Efeitos().DoFimAsync(
            Operacao(cenario, corte), MudancasDaJanela.Entre(antes, janela, cenario.Contexto(corte)), null, CancellationToken.None);
    }

    private static string[] Destinatarios(IReadOnlyList<EnviarEmail> enviados, TipoDeAviso tipo) =>
        [.. enviados.Where(e => e.Finalidade == tipo.Finalidade()).Select(static e => e.Destinatario)];

    private static JanelaDaOperacao Operacao(CenarioDeMigracao cenario, DateTimeOffset agora) =>
        new(cenario.Perfil, cenario.Janela!, cenario.Contexto(agora));

    private static AvisosDosEfeitosDaJanela Efeitos() =>
        new(
            new EmissorDeAvisos(new CenarioDaGovernancaDaVersao.RegistroDeAvisosNaMemoria(), MetricasDeTeste, NullLogger<EmissorDeAvisos>.Instance),
            new GruposDaGovernanca(new TitularesDaGovernanca()),
            new LinkDeTeste(),
            new CatalogoDaAplicacao([]));
}
