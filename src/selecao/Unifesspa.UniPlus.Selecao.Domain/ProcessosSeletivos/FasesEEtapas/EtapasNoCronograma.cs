namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A etapa acontece numa fase do cronograma: a fase que ela declara existe, a janela dela cabe na da fase e a fase que agrupa
/// etapas tem ao menos uma que declare acontecer nela. A fase repetida no cronograma ou com janela invertida não tem como
/// abrigar a conferência, e o item que a projeta fica vermelho: não há como verificá-la.
/// </summary>
public static class EtapasNoCronograma
{
    /// <summary>
    /// Toda etapa declara uma fase que está no cronograma. Com fase recusada pela leitura na lista, a fase da etapa pode ser a
    /// recusada, e a regra não julga: a causa é a da leitura.
    /// </summary>
    public static IReadOnlyList<FieldError> ConferirFaseDasEtapas(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas)
    {
        ArgumentNullException.ThrowIfNull(cronograma);
        ArgumentNullException.ThrowIfNull(etapas);

        return cronograma.TemParteRecusadaNaListaDeFases ? [] : EtapasComFaseForaDoCronograma(cronograma, etapas);
    }

    public static IReadOnlyList<FieldError> ConferirJanelaDasEtapas(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas)
    {
        ArgumentNullException.ThrowIfNull(cronograma);
        ArgumentNullException.ThrowIfNull(etapas);

        return [.. etapas.Etapas.SelectMany((etapa, posicao) => JanelaDaEtapa(posicao, etapa, FaseQueAbriga(cronograma, etapa)))];
    }

    /// <summary>
    /// A fase que agrupa etapas tem etapa que acontece nela. Com etapa sem fase declarada não se sabe onde ela acontece, e a
    /// regra inteira fica indeterminada; a fase com código repetido também fica de fora.
    /// </summary>
    public static IReadOnlyList<FieldError> ConferirFasesQueAgrupamEtapas(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas)
    {
        ArgumentNullException.ThrowIfNull(cronograma);
        ArgumentNullException.ThrowIfNull(etapas);

        return HaEtapaSemFaseDeclarada(etapas)
            ? []
            : [
                .. cronograma.Fases
                    .Select(static (fase, posicao) => (fase, posicao))
                    .Where(par => par.fase.AgrupaEtapas && !cronograma.CodigoAmbiguo(par.fase.Codigo) && !AlgumaEtapaAcontece(etapas, par.fase))
                    .Select(static par => CronogramaDoProcesso.NaFase(par.posicao, "faseCanonicaId", AvaliacaoSemEtapa())),
            ];
    }

    public static ItemConformidade ItemDaFaseQueAgrupaEtapas(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) => Item(
        "cronograma_fase_agrupadora_sem_etapa_pontuada",
        "Cronograma: toda fase que agrupa etapas tem ao menos uma etapa que acontece nela",
        ConferirFasesQueAgrupamEtapas(cronograma, etapas).Count == 0
            && !HaEtapaSemFaseDeclarada(etapas)
            && !cronograma.Fases.Any(fase => fase.AgrupaEtapas && cronograma.CodigoAmbiguo(fase.Codigo)));

    public static ItemConformidade ItemDaFaseDeclaradaPelaEtapa(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) => Item(
        "cronograma_etapa_declara_a_fase",
        "Cronograma: toda etapa declara uma fase que está no cronograma",
        ConferirFaseDasEtapas(cronograma, etapas).Count == 0
            && etapas.ConferirCompletude().Count == 0
            && !etapas.Etapas.Any(etapa => FaseDaEtapaSemComoConferir(cronograma, etapas, etapa)));

    public static ItemConformidade ItemDaJanelaDaEtapaNaFase(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) => Item(
        "cronograma_etapa_fora_da_janela_da_fase",
        "Cronograma: a etapa acontece dentro do período da fase que a abriga",
        ConferirJanelaDasEtapas(cronograma, etapas).Count == 0 && !etapas.Etapas.Any(etapa => JanelaSemComoConferir(cronograma, etapa)));

    internal static DomainError EtapaSemFaseNoCronograma() => new(
        "ProcessoSeletivo.EtapaSemFaseNoCronograma",
        "Há etapa que declara uma fase que não está no cronograma deste processo.");

    private static List<FieldError> EtapasComFaseForaDoCronograma(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
    [
        .. etapas.Etapas
            .Select(static (etapa, posicao) => (etapa, posicao))
            .Where(par => par.etapa.FaseCodigo is { } codigo && !cronograma.TemFaseDeCodigo(codigo))
            .Select(static par => EtapasDoProcesso.NaEtapa(par.posicao, "faseCodigo", EtapaSemFaseNoCronograma())),
    ];

    private static DomainError AvaliacaoSemEtapa() => new(
        "ProcessoSeletivo.AvaliacaoSemEtapa",
        "Fase que agrupa etapas no cronograma, e nenhuma etapa declara acontecer nela.");

    // A fase que a leitura recusou pode ser a da etapa: o que a regra não julga deixa o item pendente, não aprovado.
    private static bool FaseDaEtapaSemComoConferir(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, EtapaDeclarada etapa) =>
        etapa.FaseCodigo is { } codigo
            ? cronograma.TemParteRecusadaNaListaDeFases && !cronograma.TemFaseDeCodigo(codigo)
            : etapas.FaseSemJulgar(etapa);

    private static bool HaEtapaSemFaseDeclarada(EtapasDoProcesso etapas) => etapas.Etapas.Any(static etapa => etapa.FaseCodigo is null);

    private static bool AlgumaEtapaAcontece(EtapasDoProcesso etapas, FaseDeclarada fase) =>
        etapas.Etapas.Any(etapa => string.Equals(etapa.FaseCodigo, fase.Codigo, StringComparison.Ordinal));

    // A fase que abriga a etapa só serve de referência quando o código a designa sem ambiguidade e a janela dela não está invertida.
    private static FaseDeclarada? FaseQueAbriga(CronogramaDoProcesso cronograma, EtapaDeclarada etapa) =>
        etapa.FaseCodigo is { } codigo && cronograma.FaseDeCodigo(codigo) is { Janela.Invertida: false } fase ? fase : null;

    private static bool JanelaSemComoConferir(CronogramaDoProcesso cronograma, EtapaDeclarada etapa) =>
        etapa.FaseCodigo is { } codigo
        && (etapa.Janela.Inicio.HasValue || etapa.Janela.Fim.HasValue)
        && (cronograma.CodigoAmbiguo(codigo) || cronograma.FaseDeCodigo(codigo)?.Janela.Invertida == true);

    private static IEnumerable<FieldError> JanelaDaEtapa(int posicao, EtapaDeclarada etapa, FaseDeclarada? fase) =>
        fase is null
            ? []
            : [
                .. Causa(posicao, "inicio", etapa.Janela.Inicio is { } inicio ? CausaDoInicio(fase, inicio) : null),
                .. Causa(posicao, "fim", etapa.Janela.Fim is { } fim ? CausaDoFim(fase, fim) : null),
            ];

    private static IEnumerable<FieldError> Causa(int posicao, string campo, DomainError? erro) =>
        erro is null ? [] : [EtapasDoProcesso.NaEtapa(posicao, campo, erro)];

    // A etapa que declara só um extremo herda o outro da fase, e por isso cada extremo é conferido contra a janela inteira.
    private static DomainError? CausaDoInicio(FaseDeclarada fase, DateTimeOffset inicio) =>
        fase.Janela.PosicaoDe(inicio) switch
        {
            PosicaoNaJanela.Antes => new DomainError(
                "ProcessoSeletivo.EtapaComecaAntesDaFase",
                "A etapa começa antes do período da fase que a abriga: a etapa acontece dentro do período da fase."),
            PosicaoNaJanela.Depois => new DomainError(
                "ProcessoSeletivo.EtapaForaDaJanelaDaFase",
                "A etapa começa depois do fim do período da fase que a abriga: a etapa acontece dentro do período da fase."),
            _ => null,
        };

    private static DomainError? CausaDoFim(FaseDeclarada fase, DateTimeOffset fim) =>
        fase.Janela.PosicaoDe(fim) switch
        {
            PosicaoNaJanela.Depois => new DomainError(
                "ProcessoSeletivo.EtapaTerminaDepoisDaFase",
                "A etapa termina depois do período da fase que a abriga: a etapa acontece dentro do período da fase."),
            PosicaoNaJanela.Antes => new DomainError(
                "ProcessoSeletivo.EtapaForaDaJanelaDaFase",
                "A etapa termina antes do início do período da fase que a abriga: a etapa acontece dentro do período da fase."),
            _ => null,
        };

    private static ItemConformidade Item(string codigo, string mensagem, bool conforme) =>
        new(codigo, DimensaoConformidade.Cronograma, mensagem, conforme);
}
