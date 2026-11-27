namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// O período da inscrição é o que o ato publicou, o mesmo do edital e da vitrine, com início e fim
/// inclusivos.
/// </summary>
public sealed class JanelaDaInscricaoTests
{
    private static readonly TimeZoneInfo Belem = TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId);
    private static readonly DateTimeOffset Abertura = InstanteEmBelem.Em(2026, 3, 2, 8, 0);
    private static readonly DateTimeOffset Encerramento = InstanteEmBelem.Em(2026, 3, 20, 23, 59);

    private static JanelaDaInscricao Janela() => Janela(Abertura, Encerramento);

    private static JanelaDaInscricao Janela(DateTimeOffset publicadaDe, DateTimeOffset publicadaAte)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Inscrição", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        processo.DefinirCronogramaFases([FaseDeInscricao()], []).IsSuccess.Should().BeTrue();
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);

        return JanelaDaInscricao.Da(Configuracao(processo), Dados(publicadaDe, publicadaAte)).Value!;
    }

    private static FaseCronograma FaseDeInscricao() => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensDoTipoDeFaseDeTeste.Da(
            origemId: Guid.CreateVersion7(), codigo: "INSCRICAO", origemData: OrigemDataFase.NoEdital, agrupaEtapas: false,
            permiteComplementacao: false, coletaInscricao: true, coletaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: Abertura, Fim: Encerramento, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    private static DadosEdital Dados(DateTimeOffset de, DateTimeOffset ate) =>
        DadosEdital.Criar("01/2026", de, ate, Guid.CreateVersion7()).Value!;

    private static ConfiguracaoPublicada Configuracao(ProcessoSeletivo processo) => Configuracao(processo.Localidade, [.. processo.CronogramaFases], [.. processo.Formularios]);

    private static ConfiguracaoPublicada Configuracao(FaseCronograma fase, PapelDoFormularioDeTeste papel) =>
        Configuracao(ConfiguracaoPublicadaDeTeste.Localidade, [fase], [LigacaoAoFormulario.FormularioNaFase(papel, fase)]);

    private static ConfiguracaoPublicada Configuracao(LocalidadeRegente localidade, FaseCronograma[] fases, FormularioProcesso[] formularios) => new ConfiguracaoPublicada(
            new CronogramaDeclarado(localidade, null, fases),
            new AvaliacaoDeclarada([], [], VersoesPublicadasDeTeste.Versao([], []).Avaliacao.Classificacao),
            new VagasDeclaradas([], 1, false,
                null, null, AtendimentoEspecializado.Criar([], [], []).Value!),
            new ExigenciasDeclaradas([], []),
            new ColetaDeclarada([], [], formularios, [],
                [], [], null),
            new DivulgacaoETaxaDeclaradas(null, null), null);

    [Theory(DisplayName = "No início, no fim e entre os dois, a inscrição está no período")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Conferir_DentroDaJanela_Aceita(int momento)
    {
        DateTimeOffset agora = momento switch
        {
            0 => Abertura,
            1 => Abertura.AddDays(5),
            _ => Encerramento,
        };

        Janela().Conferir(agora, Belem).Should().BeNull();
    }

    [Fact(DisplayName = "O período conferido é o publicado, mesmo quando a fase do formulário tem outra janela")]
    public void Conferir_PeriodoPublicadoDiferenteDaFase_SegueOPublicado()
    {
        DateTimeOffset prorrogado = Encerramento.AddDays(10);
        JanelaDaInscricao janela = Janela(Abertura, prorrogado);

        janela.Conferir(Encerramento.AddDays(5), Belem).Should().BeNull();
        janela.Conferir(prorrogado.AddMinutes(1), Belem)!.Recusa.Code.Should().Be(InscricaoErrorCodes.ForaDoPeriodo);
    }

    [Fact(DisplayName = "Antes da abertura, as inscrições estão fechadas porque o período ainda não começou")]
    public void Conferir_AntesDaAbertura_InformaPeriodoNaoIniciado() =>
        Janela().Conferir(Abertura.AddMinutes(-1), Belem)!.Motivo.Should().Be(MotivoDasInscricoesFechadas.PeriodoNaoIniciado);

    [Fact(DisplayName = "Depois do encerramento, as inscrições estão fechadas porque o período terminou")]
    public void Conferir_DepoisDoEncerramento_InformaPeriodoEncerrado() =>
        Janela().Conferir(Encerramento.AddMinutes(1), Belem)!.Motivo.Should().Be(MotivoDasInscricoesFechadas.PeriodoEncerrado);

    [Fact(DisplayName = "No primeiro e no último instante do período não há motivo de fechamento")]
    public void Conferir_NosLimitesDoPeriodo_NaoInformaMotivo()
    {
        JanelaDaInscricao janela = Janela();

        janela.Conferir(Abertura, Belem).Should().BeNull();
        janela.Conferir(Encerramento, Belem).Should().BeNull();
    }

    [Fact(DisplayName = "Antes da abertura, a recusa diz quando a inscrição abre e até quando vai, no horário do fuso institucional")]
    public void Conferir_AntesDaAbertura_Recusa()
    {
        InscricoesFechadas? fechadas = Janela().Conferir(Abertura.AddMinutes(-1), Belem);

        fechadas!.Recusa.Code.Should().Be(InscricaoErrorCodes.ForaDoPeriodo);
        fechadas.Recusa.Message.Should().Be(
            "As inscrições deste processo seletivo começam em 02/03/2026 às 08h00 e vão até 20/03/2026 às 23h59, no horário de Belém (PA).");
    }

    [Fact(DisplayName = "Depois do encerramento, a recusa diz quando a inscrição se encerrou")]
    public void Conferir_DepoisDoEncerramento_Recusa()
    {
        InscricoesFechadas? fechadas = Janela().Conferir(Encerramento.AddMinutes(1), Belem);

        fechadas!.Recusa.Code.Should().Be(InscricaoErrorCodes.ForaDoPeriodo);
        fechadas.Recusa.Message.Should().Be("As inscrições deste processo seletivo se encerraram em 20/03/2026 às 23h59, no horário de Belém (PA).");
    }

    [Fact(DisplayName = "O formulário obrigatório numa fase que coleta inscrição faz do processo um que recebe inscrição, qualquer que seja o papel dele")]
    public void Da_FormularioObrigatorioEmFaseQueColetaInscricao_AbreAJanela()
    {
        ConfiguracaoPublicada configuracao = Configuracao(FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true), PapelDoFormularioDeTeste.Habilitacao);

        JanelaDaInscricao.Da(configuracao, Dados(Abertura, Encerramento)).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "O formulário obrigatório numa fase que não coleta inscrição não faz do processo um que recebe inscrição")]
    public void Da_FormularioObrigatorioEmFaseQueNaoColetaInscricao_Recusa()
    {
        ConfiguracaoPublicada configuracao = Configuracao(FaseDeTeste.Criar(1, "HABILITACAO"), PapelDoFormularioDeTeste.Inscricao);

        JanelaDaInscricao.Da(configuracao, Dados(Abertura, Encerramento)).Error!.Code.Should().Be(InscricaoErrorCodes.ProcessoSemInscricao);
    }

    [Fact(DisplayName = "O formulário facultativo, ainda que numa fase que coleta inscrição, não faz do processo um que recebe inscrição")]
    public void Da_FormularioFacultativo_Recusa()
    {
        ConfiguracaoPublicada configuracao = Configuracao(FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true), PapelDoFormularioDeTeste.IsencaoTaxa);

        JanelaDaInscricao.Da(configuracao, Dados(Abertura, Encerramento)).Error!.Code.Should().Be(InscricaoErrorCodes.ProcessoSemInscricao);
    }

    [Fact(DisplayName = "A versão sem formulário de inscrição não recebe inscrição pelo sistema")]
    public void Da_SemFormularioDeInscricao_Recusa()
    {
        ConfiguracaoPublicada semFormulario = VersoesPublicadasDeTeste.Versao([], []);

        Result<JanelaDaInscricao> janela = JanelaDaInscricao.Da(semFormulario, Dados(Abertura, Encerramento));

        janela.Error!.Code.Should().Be(InscricaoErrorCodes.ProcessoSemInscricao);
    }
}
