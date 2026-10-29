namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Services;
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
            "PS Inscrição", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FaseCronograma fase = FaseCronograma.Criar(
            ordem: 1, faseCanonicaOrigemId: Guid.CreateVersion7(), codigo: "INSCRICAO", donoInstitucional: "CEPS",
            origemData: OrigemDataFase.NoEdital, agrupaEtapas: false, permiteComplementacao: false,
            coletaInscricao: true, coletaSolicitacaoIsencao: false, inicio: Abertura, fim: Encerramento,
            produtos: [], faseConcluinteCodigo: null, emiteParecerIndividual: false,
            bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);

        return JanelaDaInscricao.Da(Grafo(processo), Dados(publicadaDe, publicadaAte)).Value!;
    }

    private static DadosEdital Dados(DateTimeOffset de, DateTimeOffset ate) =>
        DadosEdital.Criar("01/2026", de, ate, Guid.CreateVersion7()).Value!;

    private static GrafoConfiguracao Grafo(ProcessoSeletivo processo) => new(
        [], OfertaAtendimentoEspecializado.Criar([], [], []).Value!, [], null, [],
        VersoesPublicadasDeTeste.DoProcesso(processo).Classificacao, [.. processo.CronogramaFases], [], [], null,
        formularios: [.. processo.Formularios],
        localidade: processo.Localidade,
            numeroDeOpcoesDeCurso: 1);

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
        janela.Conferir(prorrogado.AddMinutes(1), Belem)!.Code.Should().Be(InscricaoErrorCodes.ForaDoPeriodo);
    }

    [Fact(DisplayName = "Antes da abertura, a recusa diz quando a inscrição abre e até quando vai, no horário do fuso institucional")]
    public void Conferir_AntesDaAbertura_Recusa()
    {
        DomainError? recusa = Janela().Conferir(Abertura.AddMinutes(-1), Belem);

        recusa!.Code.Should().Be(InscricaoErrorCodes.ForaDoPeriodo);
        recusa.Message.Should().Be(
            "As inscrições deste processo seletivo começam em 02/03/2026 às 08h00 e vão até 20/03/2026 às 23h59, no horário de Belém (PA).");
    }

    [Fact(DisplayName = "Depois do encerramento, a recusa diz quando a inscrição se encerrou")]
    public void Conferir_DepoisDoEncerramento_Recusa()
    {
        DomainError? recusa = Janela().Conferir(Encerramento.AddMinutes(1), Belem);

        recusa!.Code.Should().Be(InscricaoErrorCodes.ForaDoPeriodo);
        recusa.Message.Should().Be("As inscrições deste processo seletivo se encerraram em 20/03/2026 às 23h59, no horário de Belém (PA).");
    }

    [Fact(DisplayName = "A versão sem formulário de inscrição não recebe inscrição pelo sistema")]
    public void Da_SemFormularioDeInscricao_Recusa()
    {
        GrafoConfiguracao semFormulario = VersoesPublicadasDeTeste.Versao([], []);

        Result<JanelaDaInscricao> janela = JanelaDaInscricao.Da(semFormulario, Dados(Abertura, Encerramento));

        janela.Error!.Code.Should().Be(InscricaoErrorCodes.ProcessoSemInscricao);
    }
}
