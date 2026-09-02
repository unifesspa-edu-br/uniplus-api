namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

/// <summary>
/// As regras de contorno da janela de solicitação de isenção (UNI-REQ-0106): abre junto com a
/// inscrição, fecha antes dela, e dura no mínimo cinco dias corridos.
/// </summary>
public sealed class JanelaDeSolicitacaoDeIsencaoTests
{
    private static readonly TimeZoneInfo Belem = TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId);
    private static readonly DateTimeOffset AberturaDasInscricoes = new(2026, 3, 2, 0, 0, 0, TimeSpan.FromHours(-3));
    private static readonly DateTimeOffset FimDasInscricoes = new(2026, 3, 31, 23, 59, 59, TimeSpan.FromHours(-3));

    [Fact(DisplayName = "Janela que abre depois das inscrições é recusada")]
    public void AberturaDivergente_Recusada()
    {
        DomainError? pendencia = Avaliar(
            aberturaIsencao: AberturaDasInscricoes.AddMinutes(1),
            fimIsencao: new DateTimeOffset(2026, 3, 20, 23, 59, 59, TimeSpan.FromHours(-3)));

        pendencia!.Code.Should().Be("ProcessoSeletivo.JanelaDeIsencaoNaoAbreComAInscricao");
    }

    [Theory(DisplayName = "O término da isenção precisa ser estritamente anterior ao fim das inscrições")]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(-1, true)]
    public void TerminoRelativoAoFimDasInscricoes(int segundosAlemDoFim, bool aceito)
    {
        DomainError? pendencia = Avaliar(
            aberturaIsencao: AberturaDasInscricoes,
            fimIsencao: FimDasInscricoes.AddSeconds(segundosAlemDoFim));

        if (aceito)
        {
            pendencia.Should().BeNull();
            return;
        }

        pendencia!.Code.Should().Be("ProcessoSeletivo.JanelaDeIsencaoNaoTerminaAntesDaInscricao");
    }

    [Theory(DisplayName = "A janela dura no mínimo cinco dias corridos, contados do dia seguinte à abertura")]
    [InlineData(4, false)]
    [InlineData(5, true)]
    public void DuracaoMinima(int diasAteOFim, bool aceito)
    {
        // Abertura em 02/03; o dia 1 é 03/03, e o quinto se completa às 23:59:59 de 07/03.
        DateTimeOffset fim = new DateTimeOffset(
            2026, 3, 2 + diasAteOFim, 23, 59, 59, TimeSpan.FromHours(-3));

        DomainError? pendencia = Avaliar(AberturaDasInscricoes, fim);

        if (aceito)
        {
            pendencia.Should().BeNull();
            return;
        }

        pendencia!.Code.Should().Be("ProcessoSeletivo.JanelaDeIsencaoMenorQueCincoDias");
    }

    [Fact(DisplayName = "O quinto dia se completa a partir de 23:59:59 — fração de segundo não recusa a janela")]
    public void QuintoDiaComFracaoDeSegundo_Aceita()
    {
        // Ninguém digita fração de segundo, mas o sistema grava instante completo. O piso é o
        // segundo, e não o último tick, senão a janela declarada em 23:59:59 seria recusada.
        DomainError? pendencia = Avaliar(
            aberturaIsencao: AberturaDasInscricoes,
            fimIsencao: new DateTimeOffset(2026, 3, 7, 23, 59, 59, TimeSpan.FromHours(-3)).AddTicks(5_000_000));

        pendencia.Should().BeNull();
    }

    [Fact(DisplayName = "A contagem é em dias corridos: janela de cinco dias que atravessa feriado é aceita")]
    public void CincoDiasAtravessandoFeriado_Aceita()
    {
        // 21/04 é feriado nacional. Uma contagem em dias úteis exigiria janela maior; a janela é
        // período do cronograma, não interposição de recurso, e não consulta o calendário.
        DateTimeOffset abertura = new(2026, 4, 18, 0, 0, 0, TimeSpan.FromHours(-3));

        DomainError? pendencia = Avaliar(
            aberturaIsencao: abertura,
            fimIsencao: new DateTimeOffset(2026, 4, 23, 23, 59, 59, TimeSpan.FromHours(-3)),
            aberturaInscricao: abertura,
            fimInscricao: new DateTimeOffset(2026, 5, 10, 23, 59, 59, TimeSpan.FromHours(-3)));

        pendencia.Should().BeNull();
    }

    [Fact(DisplayName = "Fase de isenção sem janela definida é recusada antes das demais regras")]
    public void SemJanela_Recusada()
    {
        DomainError? pendencia = Avaliar(aberturaIsencao: null, fimIsencao: null);

        pendencia!.Code.Should().Be("ProcessoSeletivo.JanelaDeIsencaoSemPrazo");
    }

    [Fact(DisplayName = "Cronograma sem fase de isenção não é recusado — quem não cobra taxa não tem a fase")]
    public void SemFaseDeIsencao_NaoRecusa()
    {
        ProcessoSeletivo processo = ProcessoComCronograma(
            [FaseDeInscricao(AberturaDasInscricoes, FimDasInscricoes)]);

        processo.PendenciaDoCronograma(Belem).Should().BeNull();
    }

    private static DomainError? Avaliar(
        DateTimeOffset? aberturaIsencao,
        DateTimeOffset? fimIsencao,
        DateTimeOffset? aberturaInscricao = null,
        DateTimeOffset? fimInscricao = null,
        RegraRecursoFase? recurso = null)
    {
        ProcessoSeletivo processo = ProcessoComCronograma([
            FaseDeInscricao(aberturaInscricao ?? AberturaDasInscricoes, fimInscricao ?? FimDasInscricoes),
            FaseDeIsencao(aberturaIsencao, fimIsencao, recurso),
        ]);

        return processo.PendenciaDoCronograma(Belem);
    }

    private static FaseCronograma FaseDeInscricao(DateTimeOffset inicio, DateTimeOffset fim) =>
        FaseCronograma.Criar(
            ordem: 1, faseCanonicaOrigemId: Guid.CreateVersion7(), codigo: "INSCRICAO", donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Propria, agrupaEtapas: false, permiteComplementacao: false,
            produzResultado: false, resultadoDefinitivo: false,
            coletaInscricao: true, coletaSolicitacaoIsencao: false, inicio: inicio, fim: fim,
            atoProduzidoCodigo: null, atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [], regraRecurso: null).Value!;

    private static FaseCronograma FaseDeIsencao(
        DateTimeOffset? inicio, DateTimeOffset? fim, RegraRecursoFase? recurso = null) =>
        FaseCronograma.Criar(
            ordem: 2, faseCanonicaOrigemId: Guid.CreateVersion7(), codigo: "SOLICITACAO_ISENCAO", donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Delegada, agrupaEtapas: false, permiteComplementacao: false,
            produzResultado: true, resultadoDefinitivo: false,
            coletaInscricao: false, coletaSolicitacaoIsencao: true, inicio: inicio, fim: fim,
            atoProduzidoCodigo: "SOLICITACAO_ISENCAO", atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [], regraRecurso: recurso ?? RecursoEmDiasUteis(2m)).Value!;

    private static RegraRecursoFase RecursoEmDiasUteis(decimal prazo) => RegraRecursoFase.Criar(
        ReferenciaRegra.Criar(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('d', 64)).Value!,
        new ArgsRegraPrazoRecurso(
            PrazoValor: prazo,
            PrazoUnidade: UnidadePrazo.DiasUteis,
            AtoAncoraCodigo: "SOLICITACAO_ISENCAO",
            SuspensividadePrimeiraInstanciaValor: null,
            SuspensividadePrimeiraInstanciaUnidade: null,
            SuspensividadeSegundaInstanciaValor: null,
            SuspensividadeSegundaInstanciaUnidade: null)).Value!;

    private static ProcessoSeletivo ProcessoComCronograma(FaseCronograma[] fases)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Isenção", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        processo.DefinirCronogramaFases(fases, [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        return processo;
    }
}
