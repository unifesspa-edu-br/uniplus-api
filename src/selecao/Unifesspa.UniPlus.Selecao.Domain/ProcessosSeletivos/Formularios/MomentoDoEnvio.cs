namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Descreve a vez do envio do formulário. Não define a ordem do cronograma; o pedido de isenção só abre depois do envio
/// da inscrição (UNI-REQ-0149). É a posição da fase na lista, e a fase que coleta isenção, quando a lista a coloca antes
/// da fase de inscrição, envia na vez da inscrição; havendo mais de uma fase que coleta inscrição, vale a de menor
/// ordem. A marca "no mesmo momento da anterior" não muda o envio (UNI-REQ-0144).
/// </summary>
internal static class MomentoDoEnvio
{
    internal static int Da(FaseCronograma fase, IEnumerable<FaseCronograma> cronograma)
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
