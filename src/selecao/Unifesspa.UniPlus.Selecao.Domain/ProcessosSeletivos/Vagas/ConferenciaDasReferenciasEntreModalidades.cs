namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O cadastro só prova que os códigos de origem, destino, par e fallback existem; aqui se confere que
/// participam desta oferta. Uma modalidade que cita um código fora do conjunto selecionado deixaria o
/// remanejamento apontando para uma modalidade ausente. As recusas se acumulam (ADR-0125), por
/// modalidade, na ordem do conjunto. Com uma modalidade recusada pelo cadastro, o código que uma aceita cita
/// pode ser o da recusada: a citação a código fora das aceitas e a reclassificação para uma ampla concorrência
/// que não está entre as aceitas não se conferem; o par consigo mesma e o par não recíproco entre aceitas
/// continuam conferidos.
/// </summary>
public static class ConferenciaDasReferenciasEntreModalidades
{
    private static readonly (Func<ModalidadeSelecionada, string?> Citada, string Codigo, string Rotulo)[] Citacoes =
    [
        (static m => m.ComposicaoOrigemCodigo, DistribuicaoDeVagasErrorCodes.ComposicaoOrigemNaoSelecionada, "a origem"),
        (static m => m.RemanejamentoDestino, DistribuicaoDeVagasErrorCodes.RemanejamentoDestinoNaoSelecionado, "o destino de remanejamento"),
        (static m => m.RemanejamentoPar, DistribuicaoDeVagasErrorCodes.RemanejamentoParNaoSelecionado, "o par de remanejamento"),
        (static m => m.RemanejamentoFallback, DistribuicaoDeVagasErrorCodes.RemanejamentoFallbackNaoSelecionado, "o fallback de remanejamento"),
    ];

    public static IReadOnlyList<FieldError> Conferir(DistribuicaoDeVagasDeclarada declarada)
    {
        ArgumentNullException.ThrowIfNull(declarada);

        HashSet<string> codigos = [.. declarada.Modalidades.Select(static m => m.Codigo)];
        return [.. declarada.Modalidades.SelectMany(modalidade => RecusasDaModalidade(modalidade, declarada, codigos))];
    }

    private static IEnumerable<FieldError> RecusasDaModalidade(
        ModalidadeSelecionada modalidade, DistribuicaoDeVagasDeclarada declarada, HashSet<string> codigos) =>
    [
        .. CitacoesForaDoConjunto(modalidade, declarada, codigos),
        .. ParCruzadoNaoReciproco(modalidade, declarada.Modalidades),
    ];

    private static IEnumerable<FieldError> CitacoesForaDoConjunto(
        ModalidadeSelecionada modalidade, DistribuicaoDeVagasDeclarada declarada, HashSet<string> codigos) =>
        declarada.Recusado(DadoDaDistribuicaoDeVagas.Modalidades)
            ? []
            : Citacoes
                .Where(citacao => citacao.Citada(modalidade) is { } citada && !codigos.Contains(citada))
                .Select(citacao => Recusa(citacao.Codigo, $"Uma modalidade referencia {citacao.Rotulo} que não está entre as modalidades selecionadas nesta oferta."));

    // A reciprocidade é exigida aqui, e não no cadastro, porque a primeira modalidade de um par não teria
    // a que apontar quando é criada. O auto-laço satisfaria a comparação — a modalidade aponta para si e
    // encontra a si —, e um par de um nó só não remaneja para lugar nenhum.
    private static IEnumerable<FieldError> ParCruzadoNaoReciproco(
        ModalidadeSelecionada modalidade, IReadOnlyList<ModalidadeSelecionada> modalidades) =>
        modalidade is { RegraRemanejamento: RegraRemanejamentoModalidade.Cruzado, RemanejamentoPar: { } par }
            ? ConferirPar(modalidade, par, modalidades)
            : [];

    private static IEnumerable<FieldError> ConferirPar(
        ModalidadeSelecionada modalidade, string par, IReadOnlyList<ModalidadeSelecionada> modalidades) =>
        string.Equals(par, modalidade.Codigo, StringComparison.Ordinal)
            ? [Recusa(DistribuicaoDeVagasErrorCodes.ParCruzadoNaoReciproco, "Uma modalidade não pode formar par cruzado consigo mesma.")]
            : ParQueNaoApontaDeVolta(modalidade, par, modalidades);

    private static IEnumerable<FieldError> ParQueNaoApontaDeVolta(
        ModalidadeSelecionada modalidade, string par, IReadOnlyList<ModalidadeSelecionada> modalidades)
    {
        ModalidadeSelecionada? contraparte = modalidades.FirstOrDefault(m => string.Equals(m.Codigo, par, StringComparison.Ordinal));
        bool apontaDeVolta = contraparte is null
            || (contraparte.RegraRemanejamento == RegraRemanejamentoModalidade.Cruzado
                && string.Equals(contraparte.RemanejamentoPar, modalidade.Codigo, StringComparison.Ordinal));

        return apontaDeVolta
            ? []
            : [Recusa(DistribuicaoDeVagasErrorCodes.ParCruzadoNaoReciproco, "O par cruzado da modalidade deve apontar de volta para ela.")];
    }

    private static FieldError Recusa(string codigo, string mensagem) =>
        new("modalidades", new DomainError(codigo, mensagem));
}
