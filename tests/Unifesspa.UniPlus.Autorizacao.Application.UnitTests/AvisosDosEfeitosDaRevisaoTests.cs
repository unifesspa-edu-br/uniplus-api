namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.RevisoesDasConcessoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDeAvisos;

/// <summary>
/// Os avisos do que a revisão das concessões fez: a migração e a revogação ao beneficiário, o envio com recusas, as migrações
/// que aguardam o Representante do controlador e o encerramento. Só personas fictícias do portal.
/// </summary>
public sealed class AvisosDosEfeitosDaRevisaoTests
{
    private const string Emissor = TitularesDaGovernanca.Emissor;
    private const string EmailDiogo = TitularesDaGovernanca.EmailDiogo;
    private const string EmailIsabella = TitularesDaGovernanca.EmailIsabella;
    private const string EmailMarlene = TitularesDaGovernanca.EmailMarlene;
    private const string EmailLeandro = "leandro-viana84@uniplus-test.local";
    private const string EmailBetina = "betina-dasneves81@uniplus-test.local";

    private static readonly Identidade Diogo = new(Emissor, "diogo.souza");
    private static readonly Identidade Betina = new(Emissor, "betina.neves");

    // Quem envia o envio sem estar em grupo nenhum, para que todo gestor e todo Representante possam ser cobrados.
    private static readonly Identidade Rebeca = new(Emissor, "rebeca.lima");

    [Fact(DisplayName = "O envio avisa quem migrou, quem foi recusado e os grupos de decisão, menos quem o enviou")]
    public async Task Envio_AvisaBeneficiariosEGrupos()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deLeandro = cenario.Conceder(CenarioDeMigracao.Leandro, email: EmailLeandro);
        Concessao deBetina = cenario.Conceder(Betina, email: EmailBetina);
        cenario.Abrir();

        IReadOnlyList<EnviarEmail> enviados = await AvisosDoEnvioAsync(
            cenario, Diogo, true, (deLeandro, ResultadoDaMigracao.Migrar), (deBetina, ResultadoDaMigracao.Recusar));

        Destinatarios(enviados, TipoDeAviso.ConcessaoMigrada).Should().BeEquivalentTo([EmailLeandro]);
        Destinatarios(enviados, TipoDeAviso.ConcessaoRevogada).Should().BeEquivalentTo([EmailBetina]);
        Destinatarios(enviados, TipoDeAviso.DecisoesEnviadasComRecusas).Should().BeEquivalentTo([EmailIsabella, EmailMarlene]);
        enviados.Should().HaveCount(4);
    }

    [Fact(DisplayName = "As migrações que passam a aguardar o Representante do controlador avisam também o gestor que não é Representante")]
    public async Task Envio_QueAguardaAProtecao_AvisaRepresentantesEGestores()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        Concessao deLeandro = cenario.Conceder(CenarioDeMigracao.Leandro, email: EmailLeandro);
        cenario.Abrir();

        IReadOnlyList<EnviarEmail> enviados = await AvisosDoEnvioAsync(cenario, Rebeca, false, (deLeandro, ResultadoDaMigracao.Migrar));

        Destinatarios(enviados, TipoDeAviso.MigracoesAguardandoProtecaoDeDados).Should().BeEquivalentTo([EmailDiogo, EmailIsabella, EmailMarlene], "Diogo só é gestor");
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
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        deBetina.Revogar(new AtoDoAgente(Diogo, revisao.TerminaEm.AddHours(3)), "Revogação manual.").IsSuccess.Should().BeTrue();
        DateTimeOffset depois = revisao.TerminaEm.AddHours(10);
        EstadoDaRevisaoAntesDaOperacao antes = EstadoDaRevisaoAntesDaOperacao.De(revisao, cenario.Contexto(depois));
        revisao.Materializar(cenario.Contexto(depois), FormaDoEncerramento.Prazo);

        MudancasDaRevisaoDasConcessoes mudancas = MudancasDaRevisaoDasConcessoes.Entre(antes, revisao, cenario.Contexto(depois));
        IReadOnlyList<EnviarEmail> enviados = await Efeitos().DoFimAsync(Operacao(cenario, depois), mudancas, null, CancellationToken.None);

        Destinatarios(enviados, TipoDeAviso.ConcessaoRevogada).Should().BeEquivalentTo([EmailLeandro]);
        mudancas.TotalRevogadas.Should().Be(1);
    }

    [Fact(DisplayName = "Sem encerramento na operação, o fim não avisa ninguém")]
    public async Task Fim_SemEncerramento_NaoAvisa()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        cenario.Conceder(CenarioDeMigracao.Leandro, email: EmailLeandro);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        DateTimeOffset agora = CenarioDeMigracao.Abertura.AddHours(1);
        EstadoDaRevisaoAntesDaOperacao antes = EstadoDaRevisaoAntesDaOperacao.De(revisao, cenario.Contexto(agora));

        IReadOnlyList<EnviarEmail> enviados = await Efeitos().DoFimAsync(
            Operacao(cenario, agora), MudancasDaRevisaoDasConcessoes.Entre(antes, revisao, cenario.Contexto(agora)), Diogo, CancellationToken.None);

        enviados.Should().BeEmpty();
    }

    [Fact(DisplayName = "A versão anterior substituída sem revisão avisa os grupos, menos quem praticou o ato")]
    public async Task VersaoSubstituida_AvisaOsGruposMenosOAutor()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        cenario.Abrir();

        IReadOnlyList<EnviarEmail> enviados = await Efeitos().DaVersaoSubstituidaAsync(
            cenario.Perfil, cenario.VersaoAntiga, FormaDoAviso.SemConcessaoAMigrar, Diogo, CancellationToken.None);

        Destinatarios(enviados, TipoDeAviso.VersaoEncerrada).Should().BeEquivalentTo([EmailIsabella, EmailMarlene]);
    }

    // O envio é enviado pelo ator uma hora depois da abertura.
    private static async Task<IReadOnlyList<EnviarEmail>> AvisosDoEnvioAsync(
        CenarioDeMigracao cenario, Identidade ator, bool confirmou, params (Concessao Concessao, ResultadoDaMigracao Decisao)[] itens)
    {
        RevisaoDasConcessoesDoPerfil revisao = cenario.Revisao!;
        DateTimeOffset agora = CenarioDeMigracao.Abertura.AddHours(1);
        EstadoDaRevisaoAntesDaOperacao antes = EstadoDaRevisaoAntesDaOperacao.De(revisao, cenario.Contexto(agora));
        revisao.EnviarDecisoesDaGestao(CenarioDeMigracao.Envio(confirmou, itens), CenarioDeMigracao.Autoria(ator), cenario.Contexto(agora))
            .IsSuccess.Should().BeTrue();

        return await Efeitos().DaDecisaoAsync(
            Operacao(cenario, agora), MudancasDaRevisaoDasConcessoes.Entre(antes, revisao, cenario.Contexto(agora)), OperacaoNaRevisaoDasConcessoes.EnvioDaGestao, ator, CancellationToken.None);
    }

    private static async Task<IReadOnlyList<EnviarEmail>> AvisosDoFimPeloPrazoAsync(CenarioDeMigracao cenario)
    {
        RevisaoDasConcessoesDoPerfil revisao = cenario.Revisao!;
        DateTimeOffset corte = revisao.TerminaEm;
        EstadoDaRevisaoAntesDaOperacao antes = EstadoDaRevisaoAntesDaOperacao.De(revisao, cenario.Contexto(corte));
        revisao.ConferirEstado(cenario.Contexto(corte));

        return await Efeitos().DoFimAsync(
            Operacao(cenario, corte), MudancasDaRevisaoDasConcessoes.Entre(antes, revisao, cenario.Contexto(corte)), null, CancellationToken.None);
    }

    private static string[] Destinatarios(IReadOnlyList<EnviarEmail> enviados, TipoDeAviso tipo) =>
        [.. enviados.Where(e => e.Finalidade == tipo.Finalidade()).Select(static e => e.Destinatario)];

    private static RevisaoDaOperacao Operacao(CenarioDeMigracao cenario, DateTimeOffset agora) =>
        new(cenario.Perfil, cenario.Revisao!, cenario.Contexto(agora));

    private static AvisosDosEfeitosDaRevisao Efeitos() =>
        new(
            new EmissorDeAvisos(new CenarioDaGovernancaDaVersao.RegistroDeAvisosNaMemoria(), MetricasDeTeste, NullLogger<EmissorDeAvisos>.Instance),
            new GruposDaGovernanca(new TitularesDaGovernanca()),
            new LinkDeTeste(),
            new CatalogoDaAplicacao([]));
}
