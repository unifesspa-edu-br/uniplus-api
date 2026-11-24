namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// As regras de contorno da janela de solicitação de isenção (UNI-REQ-0106): abre junto com a
/// inscrição, fecha antes dela, e dura no mínimo cinco dias corridos.
/// </summary>
public sealed class JanelaDeSolicitacaoDeIsencaoTests
{
    private static DomainError? PendenciaDoCronograma(ConfiguracaoDeclarada configuracao, TimeZoneInfo? fuso) =>
        CronogramaDoProcesso.Do(configuracao.Cronograma.CronogramaFases).ConferirCompletude(fuso) is [var primeira, ..] ? primeira.Error : null;

    private static readonly TimeZoneInfo Belem = TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId);
    private static readonly DateTimeOffset AberturaDasInscricoes = InstanteEmBelem.Em(2026, 3, 2);
    private static readonly DateTimeOffset FimDasInscricoes = InstanteEmBelem.Em(2026, 3, 31, 23, 59, 59);

    [Fact(DisplayName = "Janela que abre depois das inscrições é recusada")]
    public void AberturaDivergente_Recusada()
    {
        DomainError? pendencia = Avaliar(
            aberturaIsencao: AberturaDasInscricoes.AddMinutes(1),
            fimIsencao: InstanteEmBelem.Em(2026, 3, 20, 23, 59, 59));

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
        DateTimeOffset fim = InstanteEmBelem.Em(2026, 3, 2 + diasAteOFim, 23, 59, 59);

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
            fimIsencao: InstanteEmBelem.Em(2026, 3, 7, 23, 59, 59).AddTicks(5_000_000));

        pendencia.Should().BeNull();
    }

    [Fact(DisplayName = "A contagem é em dias corridos: janela de cinco dias que atravessa feriado é aceita")]
    public void CincoDiasAtravessandoFeriado_Aceita()
    {
        // 21/04 é feriado nacional. Uma contagem em dias úteis exigiria janela maior; a janela é
        // período do cronograma, não interposição de recurso, e não consulta o calendário.
        DateTimeOffset abertura = InstanteEmBelem.Em(2026, 4, 18);

        DomainError? pendencia = Avaliar(
            aberturaIsencao: abertura,
            fimIsencao: InstanteEmBelem.Em(2026, 4, 23, 23, 59, 59),
            aberturaInscricao: abertura,
            fimInscricao: InstanteEmBelem.Em(2026, 5, 10, 23, 59, 59));

        pendencia.Should().BeNull();
    }

    [Fact(DisplayName = "Fase de isenção sem janela definida é recusada antes das demais regras")]
    public void SemJanela_Recusada()
    {
        DomainError? pendencia = Avaliar(aberturaIsencao: null, fimIsencao: null);

        pendencia!.Code.Should().Be("ProcessoSeletivo.JanelaDeIsencaoSemPrazo");
    }

    [Fact(DisplayName = "A janela irregular fica pendente no painel de conformidade, e não só na publicação")]
    public void JanelaIrregular_ItemPendenteNoPainel()
    {
        // O painel de conformidade coincide com as conferências: sem o item, o preflight diria que está tudo
        // pronto instantes antes de a publicação recusar o mesmo processo.
        ProcessoSeletivo processo = ProcessoComCronograma([
            FaseDeInscricao(AberturaDasInscricoes, FimDasInscricoes),
            FaseDeIsencao(AberturaDasInscricoes.AddMinutes(1), InstanteEmBelem.Em(2026, 3, 20, 23, 59, 59)),
        ]);

        var contexto = new ContextoDeContagemDePrazos(
            CalendarioVigente: null, FusoInstitucional: Belem);

        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Com(contexto, motivosEscolhidos: MotivosDeDecisaoDoProcessoDeTeste.Cobrindo(processo)))
            .Should().Contain(i => i.Codigo == "cronograma_janela_de_isencao" && !i.Ok);
    }

    [Fact(DisplayName = "Sem zona resolvida a conferência lança, em vez de aceitar janela de um dia")]
    public void SemFusoResolvido_NaoAceitaJanelaCurta()
    {
        // Tratar a ausência do fuso como permissão transformaria um defeito de instalação em
        // publicação de janela irregular.
        ProcessoSeletivo processo = ProcessoComCronograma([
            FaseDeInscricao(AberturaDasInscricoes, FimDasInscricoes),
            FaseDeIsencao(AberturaDasInscricoes, InstanteEmBelem.Em(2026, 3, 3, 23, 59, 59)),
        ]);

        Action semZona = () => PendenciaDoCronograma(processo.Declaracao(), null);

        semZona.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "Sem fase de inscrição, a duração mínima continua valendo")]
    public void SemFaseDeInscricao_DuracaoAindaEValidada()
    {
        // Certame de importação externa não tem fase de coleta, então as regras de posição não têm
        // a que se referir — mas os cinco dias independem da inscrição.
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Importado", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

        processo.DefinirCronogramaFases(
            [FaseDeIsencao(AberturaDasInscricoes, InstanteEmBelem.Em(2026, 3, 3, 23, 59, 59))],
            []).IsSuccess.Should().BeTrue();

        PendenciaDoCronograma(processo.Declaracao(), Belem)!.Code
            .Should().Be("ProcessoSeletivo.JanelaDeIsencaoMenorQueCincoDias");
    }

    [Fact(DisplayName = "Cronograma sem fase de isenção não é recusado — quem não cobra taxa não tem a fase")]
    public void SemFaseDeIsencao_NaoRecusa()
    {
        ProcessoSeletivo processo = ProcessoComCronograma(
            [FaseDeInscricao(AberturaDasInscricoes, FimDasInscricoes)]);

        PendenciaDoCronograma(processo.Declaracao(), Belem).Should().BeNull();
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

        return PendenciaDoCronograma(processo.Declaracao(), Belem);
    }

    private static FaseCronograma FaseDeInscricao(DateTimeOffset inicio, DateTimeOffset fim) =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
                OrigemId: Guid.CreateVersion7(), Codigo: "INSCRICAO", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: true,
                ColetaSolicitacaoIsencao: false)),
            new DecisoesDeCronogramaDaFase(Ordem: 1, Inicio: inicio, Fim: fim, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;

    private static FaseCronograma FaseDeIsencao(
        DateTimeOffset? inicio, DateTimeOffset? fim, RegraRecursoFase? recurso = null) =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
                OrigemId: Guid.CreateVersion7(), Codigo: "SOLICITACAO_ISENCAO", OrigemData: OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false,
                ColetaSolicitacaoIsencao: true)),
            new DecisoesDeCronogramaDaFase(Ordem: 2, Inicio: inicio, Fim: fim, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(
                Produtos: [
                    ProdutoDaFase.Reidratar(ProdutoPreliminarId, "RESULTADO_ISENCAO", PapelProdutoFase.Preliminar),
                    ProdutoDaFase.Criar("RESULTADO_ISENCAO", PapelProdutoFase.Definitivo),
                ],
                EmiteParecerIndividual: false,
                RegraRecurso: recurso ?? RecursoEmDiasUteis(2m)),
            bancasRequeridas: []).Value!;

    /// <summary>Produto preliminar da fase de isenção — a âncora que a regra de recurso declara.</summary>
    private static readonly Guid ProdutoPreliminarId = new("77770000-0000-4000-8000-000000000003");

    private static RegraRecursoFase RecursoEmDiasUteis(decimal prazo) => RegraRecursoFase.Criar(
        ReferenciaRegra.De(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('d', 64)),
        ParametrosDoRecursoDeTeste.Declarados(prazo, UnidadePrazo.DiasUteis),
        ProdutoPreliminarId).Value!;

    private static ProcessoSeletivo ProcessoComCronograma(FaseCronograma[] fases)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Isenção", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

        processo.DefinirCronogramaFases(fases, []).IsSuccess.Should().BeTrue();

        return processo;
    }
}
