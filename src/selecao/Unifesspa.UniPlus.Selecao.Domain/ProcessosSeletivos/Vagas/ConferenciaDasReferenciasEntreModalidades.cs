namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O cadastro só prova que os códigos de origem, destino, par e fallback existem; aqui se confere que
/// participam desta oferta. Uma modalidade que cita um código fora do conjunto selecionado deixaria o
/// remanejamento apontando para uma modalidade ausente. As recusas se acumulam (ADR-0125), por
/// modalidade, na ordem do conjunto.
/// </summary>
public static class ConferenciaDasReferenciasEntreModalidades
{
    /// <summary>Token de contrato da ação que reclassifica na ampla concorrência.</summary>
    private const string ReclassificarNaAmplaConcorrencia = "RECLASSIFICAR_AC";

    private static readonly (Func<ModalidadeSelecionada, string?> Citada, string Codigo, string Rotulo)[] Citacoes =
    [
        (static m => m.ComposicaoOrigemCodigo, DistribuicaoDeVagasErrorCodes.ComposicaoOrigemNaoSelecionada, "a origem"),
        (static m => m.RemanejamentoDestino, DistribuicaoDeVagasErrorCodes.RemanejamentoDestinoNaoSelecionado, "o destino de remanejamento"),
        (static m => m.RemanejamentoPar, DistribuicaoDeVagasErrorCodes.RemanejamentoParNaoSelecionado, "o par de remanejamento"),
        (static m => m.RemanejamentoFallback, DistribuicaoDeVagasErrorCodes.RemanejamentoFallbackNaoSelecionado, "o fallback de remanejamento"),
    ];

    public static IReadOnlyList<FieldError> Conferir(IReadOnlyList<ModalidadeSelecionada> modalidades)
    {
        ArgumentNullException.ThrowIfNull(modalidades);

        HashSet<string> codigos = [.. modalidades.Select(static m => m.Codigo)];
        return [.. modalidades.SelectMany(modalidade => RecusasDaModalidade(modalidade, modalidades, codigos))];
    }

    private static IEnumerable<FieldError> RecusasDaModalidade(
        ModalidadeSelecionada modalidade, IReadOnlyList<ModalidadeSelecionada> modalidades, HashSet<string> codigos) =>
    [
        .. Citacoes
            .Where(citacao => citacao.Citada(modalidade) is { } citada && !codigos.Contains(citada))
            .Select(citacao => Recusa(citacao.Codigo, $"Modalidade {modalidade.Codigo} referencia {citacao.Rotulo} {citacao.Citada(modalidade)}, que não está selecionado nesta oferta.")),
        .. ParCruzadoNaoReciproco(modalidade, modalidades),
        .. ReclassificacaoSemAmplaOfertada(modalidade, codigos),
    ];

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
            ? [Recusa(DistribuicaoDeVagasErrorCodes.ParCruzadoNaoReciproco, $"Modalidade {modalidade.Codigo} forma par cruzado consigo mesma.")]
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
            : [Recusa(DistribuicaoDeVagasErrorCodes.ParCruzadoNaoReciproco, $"Modalidade {modalidade.Codigo} forma par cruzado com {par}, que não aponta de volta para ela.")];
    }

    // O destino não é declarado pelo operador: está implícito no token. Sem a recusa, um certame que não
    // oferta ampla concorrência congela no envelope — imutável — uma instrução de reclassificar para
    // modalidade que ele não tem.
    private static IEnumerable<FieldError> ReclassificacaoSemAmplaOfertada(ModalidadeSelecionada modalidade, HashSet<string> codigos) =>
        string.Equals(modalidade.AcaoQuandoIndeferido, ReclassificarNaAmplaConcorrencia, StringComparison.Ordinal)
        && !codigos.Contains(ModalidadesFederaisLei12711.Ac)
            ? [Recusa(DistribuicaoDeVagasErrorCodes.ReclassificacaoParaAmplaSemAmplaOfertada,
                $"Modalidade {modalidade.Codigo} reclassifica em {ModalidadesFederaisLei12711.Ac}, que não está selecionada nesta oferta.")]
            : [];

    private static FieldError Recusa(string codigo, string mensagem) =>
        new("modalidades", new DomainError(codigo, mensagem));
}
