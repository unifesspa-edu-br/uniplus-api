namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As regras de contorno da janela de solicitação de isenção, conferidas em toda fase que a coleta: abre junto com a
/// inscrição, fecha antes dela e dura no mínimo cinco dias corridos. A contagem é em dias corridos e não passa pelo
/// calendário de dias úteis nem pelo algoritmo de contagem do processo, porque a janela é período do cronograma e não
/// interposição de recurso.
/// </summary>
internal static class JanelaDeIsencao
{
    internal static IReadOnlyList<FieldError> Recusas(CronogramaDoProcesso cronograma, TimeZoneInfo? fuso) =>
    [
        .. FasesQueColetamIsencao(cronograma)
            .SelectMany(par => DaFase(par.Posicao, par.Fase, cronograma.FaseQueAncoraOPeriodoDeInscricao, fuso)),
    ];

    /// <summary>
    /// Sem fuso o item fica verde: quem recusa a publicação é o fuso institucional não reconhecido, já vermelho pelo mesmo
    /// motivo. Com fuso, fica vermelho também quando alguma janela a conferir está invertida, porque não há como verificá-la.
    /// </summary>
    internal static bool Conforme(CronogramaDoProcesso cronograma, TimeZoneInfo? fuso) =>
        fuso is null
        || (Recusas(cronograma, fuso).Count == 0
            && !FasesQueColetamIsencao(cronograma).Any(par => NaoPodeSerConferida(par.Fase, cronograma.FaseQueAncoraOPeriodoDeInscricao)));

    private static IEnumerable<(int Posicao, FaseDeclarada Fase)> FasesQueColetamIsencao(CronogramaDoProcesso cronograma) =>
        cronograma.Fases.Select((fase, posicao) => (posicao, fase)).Where(static par => par.fase.ColetaSolicitacaoIsencao);

    private static bool NaoPodeSerConferida(FaseDeclarada fase, FaseDeclarada? inscricao) =>
        fase.Janela.Invertida || (fase.Janela.Completa && inscricao?.Janela.Invertida == true);

    private static IEnumerable<FieldError> DaFase(int posicao, FaseDeclarada fase, FaseDeclarada? inscricao, TimeZoneInfo? fuso)
    {
        if (fase.Janela.ExtremoAusente is { } extremoAusente)
        {
            return [SemPrazo(posicao, fase, extremoAusente)];
        }

        if (fase.Janela.Invertida)
        {
            return [];
        }

        TimeZoneInfo fusoDaContagem = fuso ?? throw new InvalidOperationException(
            "A duração do período de isenção depende do fuso institucional, e ele não foi informado na verificação do cronograma.");

        return [.. MenorQueCincoDias(posicao, fase, fusoDaContagem), .. ContornoDaInscricao(posicao, fase, inscricao)];
    }

    private static FieldError SemPrazo(int posicao, FaseDeclarada fase, string extremoAusente) =>
        CronogramaDoProcesso.NaFase(
            posicao,
            extremoAusente,
            new DomainError(
                "ProcessoSeletivo.JanelaDeIsencaoSemPrazo",
                $"A fase {fase.Citacao} abre a solicitação de isenção e precisa de início e fim definidos."));

    // A duração é independente da inscrição: um certame de importação externa não tem fase de coleta, e sem esta conferência
    // publicaria janela de um dia sem que nada acusasse.
    private static IEnumerable<FieldError> MenorQueCincoDias(int posicao, FaseDeclarada fase, TimeZoneInfo fuso) =>
        fase.Janela is { Inicio: { } abertura, Fim: { } fim } && fim < UltimoInstanteDoQuintoDia(abertura, fuso)
            ? [CronogramaDoProcesso.NaFase(
                posicao,
                "fim",
                new DomainError(
                    "ProcessoSeletivo.JanelaDeIsencaoMenorQueCincoDias",
                    "O período de solicitação de isenção precisa de ao menos cinco dias corridos, contados a partir do dia seguinte à abertura."))]
            : [];

    // As duas regras seguintes posicionam a janela em relação às inscrições, e só existem quando há inscrição a que se
    // referir, com janela completa e não invertida.
    private static IEnumerable<FieldError> ContornoDaInscricao(int posicao, FaseDeclarada fase, FaseDeclarada? inscricao) =>
        inscricao is { Janela: { Completa: true, Invertida: false } janelaDaInscricao }
            ? [.. NaoAbreComAInscricao(posicao, fase, janelaDaInscricao), .. NaoTerminaAntesDaInscricao(posicao, fase, janelaDaInscricao)]
            : [];

    private static IEnumerable<FieldError> NaoAbreComAInscricao(int posicao, FaseDeclarada fase, JanelaDeTempo inscricao) =>
        fase.Janela.Inicio != inscricao.Inicio
            ? [CronogramaDoProcesso.NaFase(
                posicao,
                "inicio",
                new DomainError(
                    "ProcessoSeletivo.JanelaDeIsencaoNaoAbreComAInscricao",
                    "O período de solicitação de isenção abre no mesmo instante em que abrem as inscrições."))]
            : [];

    private static IEnumerable<FieldError> NaoTerminaAntesDaInscricao(int posicao, FaseDeclarada fase, JanelaDeTempo inscricao) =>
        fase.Janela.Fim >= inscricao.Fim
            ? [CronogramaDoProcesso.NaFase(
                posicao,
                "fim",
                new DomainError(
                    "ProcessoSeletivo.JanelaDeIsencaoNaoTerminaAntesDaInscricao",
                    "A solicitação de isenção precisa terminar antes do fim das inscrições — quem tem o pedido indeferido ainda se inscreve pagando."))]
            : [];

    /// <summary>
    /// O instante em que o quinto dia corrido se completa: o dia da abertura é excluído, o primeiro é o dia seguinte, e o quinto
    /// só termina no último instante do dia, no fuso institucional. O piso é 23:59:59, não o último tick do dia: a comparação
    /// é <c>&gt;=</c>, então uma janela gravada com fração de segundo continua aceita, e exigir o tick final recusaria o caso
    /// comum, porque é em segundos que o operador declara a janela e que o envelope a congela.
    /// </summary>
    private static DateTimeOffset UltimoInstanteDoQuintoDia(DateTimeOffset abertura, TimeZoneInfo fuso)
    {
        DateTime fimDoQuintoDiaLocal = TimeZoneInfo.ConvertTime(abertura, fuso).Date.AddDays(6).AddSeconds(-1);

        return new DateTimeOffset(fimDoQuintoDiaLocal, fuso.GetUtcOffset(fimDoQuintoDiaLocal));
    }
}
