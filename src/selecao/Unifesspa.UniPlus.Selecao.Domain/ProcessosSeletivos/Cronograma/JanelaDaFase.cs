namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A janela da fase é guardada em UTC, qualquer que seja o offset com que o instante chegou: o offset
/// de quem escreve é forma de transporte, não dado de domínio, e a coluna <c>timestamp with time zone</c>
/// só aceita a representação em UTC. Aqui também as recusas da janela.
/// </summary>
internal static class JanelaDaFase
{
    internal static List<FieldError> Violacoes(
        OrigemDataFase origemData, DecisoesDeCronogramaDaFase cronograma)
    {
        DateTimeOffset? inicio = JanelaDeTempo.EmUtc(cronograma.Inicio);
        DateTimeOffset? fim = JanelaDeTempo.EmUtc(cronograma.Fim);
        FieldError?[] violacoes =
        [
            JanelaInvertida(inicio, fim),
            JanelaObrigatoriaNoEdital(origemData, inicio, fim),
        ];

        return [.. violacoes.OfType<FieldError>()];
    }

    // Vale independentemente da origem da data: uma fase NA_EXECUCAO com janela declarada não escapa da
    // checagem de coerência interna.
    private static FieldError? JanelaInvertida(DateTimeOffset? inicio, DateTimeOffset? fim) =>
        inicio is { } inicioValor && fim is { } fimValor && fimValor < inicioValor
            ? new FieldError("fim", new DomainError(
                "FaseCronograma.JanelaInvertida",
                "A data de término do período é anterior à data de início."))
            : null;

    // NO_EDITAL exige janela; NA_EXECUCAO aceita "sem data" como estado válido, porque a unidade
    // responsável a define na execução.
    private static FieldError? JanelaObrigatoriaNoEdital(
        OrigemDataFase origemData, DateTimeOffset? inicio, DateTimeOffset? fim) =>
        origemData == OrigemDataFase.NoEdital && (inicio is null || fim is null)
            ? new FieldError(null, new DomainError(
                "FaseCronograma.JanelaObrigatoriaEmDataNoEdital",
                "A fase com data fixada no edital exige início e término do período."))
            : null;
}
