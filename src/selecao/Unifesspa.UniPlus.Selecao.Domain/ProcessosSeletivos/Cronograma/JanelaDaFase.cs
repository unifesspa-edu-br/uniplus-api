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
    internal static DateTimeOffset? EmUtc(DateTimeOffset? instante) => instante?.ToUniversalTime();

    // A janela é fixada em UTC antes de montar as mensagens, para que a violação e o valor gravado falem
    // da mesma representação do instante.
    internal static List<FieldError> Violacoes(
        string codigo, OrigemDataFase origemData, DecisoesDeCronogramaDaFase cronograma)
    {
        DateTimeOffset? inicio = EmUtc(cronograma.Inicio);
        DateTimeOffset? fim = EmUtc(cronograma.Fim);
        FieldError?[] violacoes =
        [
            JanelaInvertida(codigo, inicio, fim),
            JanelaObrigatoriaNoEdital(codigo, origemData, inicio, fim),
        ];

        return [.. violacoes.OfType<FieldError>()];
    }

    // Vale independentemente da origem da data: uma fase NA_EXECUCAO com janela declarada não escapa da
    // checagem de coerência interna.
    private static FieldError? JanelaInvertida(string codigo, DateTimeOffset? inicio, DateTimeOffset? fim) =>
        inicio is { } inicioValor && fim is { } fimValor && fimValor < inicioValor
            ? new FieldError("fim", new DomainError(
                "FaseCronograma.JanelaInvertida",
                $"A fase '{codigo}' tem o fim da janela ({fimValor:O}) antes do início ({inicioValor:O})."))
            : null;

    // NO_EDITAL exige janela; NA_EXECUCAO aceita "sem data" como estado válido, porque a unidade
    // responsável a define na execução.
    private static FieldError? JanelaObrigatoriaNoEdital(
        string codigo, OrigemDataFase origemData, DateTimeOffset? inicio, DateTimeOffset? fim) =>
        origemData == OrigemDataFase.NoEdital && (inicio is null || fim is null)
            ? new FieldError(null, new DomainError(
                "FaseCronograma.JanelaObrigatoriaEmDataNoEdital",
                $"A fase '{codigo}' tem a data fixada no edital e exige início e fim da janela."))
            : null;
}
