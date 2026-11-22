namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A escala em que a nota da etapa é lançada: a nota máxima e o passo do lançamento, e a contradição entre
/// eles e a nota mínima. Recusa a contradição em rascunho também: a falta é tolerada (o gate de publicação
/// a cobra), mas dois valores que não convivem não são falta.
/// </summary>
internal static class EscalaDoLancamento
{
    /// <summary>
    /// Acumula as recusas da escala: primeiro os valores isolados (nota máxima, passo), depois os que
    /// dependem da nota máxima. A nota mínima chega nula quando outra regra já a recusou isolada, e não é
    /// repetida nas comparações; a nota máxima inválida também não é comparada, o que evita dividir por
    /// zero no múltiplo do passo.
    /// </summary>
    internal static List<FieldError> Validar(decimal? notaMinimaAceita, decimal? notaMaxima, decimal? passoDoLancamento)
    {
        FieldError?[] isoladas = [RecusaDaNotaMaxima(notaMaxima), RecusaDoPasso(passoDoLancamento)];
        List<FieldError> erros = [.. isoladas.OfType<FieldError>()];
        if (notaMaxima is > 0)
        {
            erros.AddRange(RecusasDaComparacao(notaMinimaAceita, notaMaxima, passoDoLancamento).OfType<FieldError>());
        }

        FieldError?[] precisao =
        [
            PrecisaoDecimal.Recusa(notaMaxima, "notaMaxima", PrecisaoDecimal.NotaMaxima),
            PrecisaoDecimal.Recusa(passoDoLancamento, "passoDoLancamento", PrecisaoDecimal.Passo),
        ];
        erros.AddRange(precisao.OfType<FieldError>());
        return erros;
    }

    private static FieldError? RecusaDaNotaMaxima(decimal? notaMaxima) =>
        notaMaxima is <= 0
            ? new("notaMaxima", new DomainError(
                EtapaProcessoErrorCodes.NotaMaximaInvalida, "A nota máxima da etapa, quando informada, deve ser maior que zero."))
            : null;

    private static FieldError? RecusaDoPasso(decimal? passoDoLancamento) =>
        passoDoLancamento is <= 0
            ? new("passoDoLancamento", new DomainError(
                EtapaProcessoErrorCodes.PassoDoLancamentoInvalido, "O passo do lançamento, quando informado, deve ser maior que zero."))
            : null;

    private static FieldError?[] RecusasDaComparacao(decimal? notaMinimaAceita, decimal? notaMaxima, decimal? passoDoLancamento) =>
    [
        notaMinimaAceita > notaMaxima
            ? new("notaMinima", new DomainError(
                EtapaProcessoErrorCodes.NotaMinimaAcimaDaNotaMaxima, "A nota mínima da etapa não pode passar da nota máxima."))
            : null,
        passoDoLancamento > 0 && notaMaxima % passoDoLancamento != 0
            ? new("notaMaxima", new DomainError(
                EtapaProcessoErrorCodes.NotaMaximaForaDoPasso, "A nota máxima da etapa deve ser múltipla do passo do lançamento."))
            : null,
    ];
}
