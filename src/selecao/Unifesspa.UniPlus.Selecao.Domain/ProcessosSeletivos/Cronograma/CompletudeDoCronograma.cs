namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que o cronograma precisa ter para ser publicado, além da estrutura: a janela da fase que ancora a inscrição, a unidade
/// responsável de cada fase e a janela de isenção. Cada causa sai na posição da fase a que se refere.
/// </summary>
internal static class CompletudeDoCronograma
{
    internal static IReadOnlyList<FieldError> Recusas(CronogramaDoProcesso cronograma, TimeZoneInfo? fuso) =>
    [
        .. JanelaDaInscricaoAusente(cronograma),
        .. UnidadesResponsaveisAusentes(cronograma),
        .. JanelaDeIsencao.Recusas(cronograma, fuso),
    ];

    internal static IReadOnlyList<ItemConformidade> Itens(CronogramaDoProcesso cronograma, TimeZoneInfo? fuso) =>
    [
        Item(
            "cronograma_fase_que_coleta_inscricao_sem_janela",
            "Cronograma: a fase que coleta inscrição tem início e fim definidos",
            JanelaDaInscricaoAusente(cronograma).Any()),
        Item(
            "cronograma_fase_sem_unidade_responsavel",
            "Cronograma: toda fase tem a unidade responsável definida",
            UnidadesResponsaveisAusentes(cronograma).Any() || cronograma.Fases.Any(UnidadeRecusada)),
        Item(
            "cronograma_janela_de_isencao",
            "Cronograma: a janela de solicitação de isenção abre com a inscrição, fecha antes dela e dura cinco dias",
            !JanelaDeIsencao.Conforme(cronograma, fuso)),
    ];

    // A fase que coleta inscrição declara o prazo que o edital publica, então precisa de janela mesmo quando a data é definida
    // na execução. É a mesma fase que ancora o período, e não "alguma fase de coleta": fossem conjuntos diferentes, a projeção
    // poderia tirar o período de uma fase enquanto a recusa olhasse outra.
    private static IEnumerable<FieldError> JanelaDaInscricaoAusente(CronogramaDoProcesso cronograma) =>
        cronograma.FaseQueAncoraOPeriodoDeInscricao is { } ancora && ancora.Janela.ExtremoAusente is { } extremoAusente
            ? [CronogramaDoProcesso.NaFase(
                cronograma.PosicaoDe(ancora),
                extremoAusente,
                new DomainError(
                    "ProcessoSeletivo.FaseQueColetaInscricaoSemJanela",
                    $"A fase '{ancora.Codigo}' coleta inscrição e precisa de início e fim definidos para que o Edital declare o período."))]
            : [];

    private static IEnumerable<FieldError> UnidadesResponsaveisAusentes(CronogramaDoProcesso cronograma) =>
        cronograma.Fases
            .Select((fase, posicao) => (fase, posicao))
            .Where(static par => !par.fase.TemUnidadeResponsavel && !UnidadeRecusada(par.fase))
            .Select(par => CronogramaDoProcesso.NaFase(
                par.posicao,
                FaseDeclarada.CampoDaUnidadeResponsavel,
                new DomainError(
                    "ProcessoSeletivo.FaseSemUnidadeResponsavel",
                    $"A fase '{par.fase.Codigo}' não tem unidade responsável definida — toda fase publicada responde a uma unidade da Unifesspa.")));

    // A unidade que a leitura recusou não está ausente: a causa é a da leitura, e o item do painel fica pendente em vez de aprovado.
    private static bool UnidadeRecusada(FaseDeclarada fase) => fase.ParteRecusadaEm(FaseDeclarada.CampoDaUnidadeResponsavel);

    private static ItemConformidade Item(string codigo, string mensagem, bool reprovado) =>
        new(codigo, DimensaoConformidade.Cronograma, mensagem, !reprovado);
}
