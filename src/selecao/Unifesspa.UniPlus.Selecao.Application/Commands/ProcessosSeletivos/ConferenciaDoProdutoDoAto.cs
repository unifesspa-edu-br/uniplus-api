namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Enums;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// As recusas de um produto declarado por etapa ou por fase: o papel que não existe, o tipo de ato sem
/// versão vigente, o papel em ato que não é resultado e a violação do resultado contra o catálogo. A regra é
/// a mesma para os dois donos; muda o rótulo da recusa, e o que cada dono faz com ela — a etapa para na
/// primeira, a fase acumula.
/// </summary>
internal static class ConferenciaDoProdutoDoAto
{
    /// <summary>
    /// O papel desconhecido é RECUSADO, e não tratado como ausente: ignorá-lo fazia um erro de digitação
    /// virar produto sem papel, e quem declarou nunca saberia que o recurso perdeu onde ancorar.
    /// </summary>
    public static DomainError PapelDesconhecido(string? papel, RotuloDoDonoDoProduto dono) =>
        new(
            dono.CodigoDoPapelDesconhecido,
            $"O papel '{papel}'{dono.Qualificador} não é declarável — use '{PapelProdutoFaseCodigo.Preliminar}', '{PapelProdutoFaseCodigo.Definitivo}' ou nenhum.");

    public static DomainError AtoNaoEncontrado(string atoCodigo, RotuloDoDonoDoProduto dono) =>
        new(
            dono.CodigoDoAtoNaoEncontrado,
            $"O tipo de ato '{atoCodigo}'{dono.Qualificador} não tem versão vigente no catálogo de Publicações na data de hoje.");

    /// <summary>
    /// Papel preliminar ou definitivo só faz sentido sobre resultado, que é o par a que a janela recursal se
    /// ancora; depois, o resultado sempre declara papel e o tipo que congela a configuração não serve de item
    /// de resultado (UNI-REQ-0157).
    /// </summary>
    public static DomainError? RecusaDoPapel(
        string atoCodigo, PapelProdutoFase? papel, ClassificacaoDoTipoDeAto classificacao, RotuloDoDonoDoProduto dono) =>
        papel is not null && !classificacao.EhResultado
            ? new DomainError(
                dono.CodigoDoPapelEmAtoQueNaoEhResultado,
                $"O tipo de ato '{atoCodigo}'{dono.Qualificador} não é resultado no catálogo — só resultado recebe papel preliminar ou definitivo.")
            : classificacao.ViolacaoDoProduto(atoCodigo, papel, dono.Lugar);
}
