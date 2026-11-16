namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// O momento da fase no cronograma (UNI-REQ-0106, UNI-REQ-0144): a ordem dela. A fase que abre a
/// solicitação de isenção é contemporânea da inscrição e, quando a ordem a coloca antes, tem o momento
/// da fase de inscrição; havendo mais de uma fase que coleta inscrição, vale a de menor ordem. Toda
/// comparação "igual ou anterior" entre fases usa o momento, e nunca o código ou o nome da fase.
/// </summary>
public static class MomentoDaFase
{
    public static int Da(FaseCronograma fase, IEnumerable<FaseCronograma> cronograma)
    {
        ArgumentNullException.ThrowIfNull(fase);
        ArgumentNullException.ThrowIfNull(cronograma);

        return fase.ColetaSolicitacaoIsencao && OrdemDaPrimeiraFaseDeInscricao(cronograma) is { } ordemDaInscricao
            ? Math.Max(fase.Ordem, ordemDaInscricao)
            : fase.Ordem;
    }

    private static int? OrdemDaPrimeiraFaseDeInscricao(IEnumerable<FaseCronograma> cronograma) =>
        cronograma.Where(static fase => fase.ColetaInscricao).Select(static fase => (int?)fase.Ordem).Min();
}
