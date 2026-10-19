namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// O que o catálogo vigente de Publicações diz de um tipo de ato que o cronograma declara, e que
/// decide se o tipo pode ser item de resultado: se ele É resultado e se congela a configuração.
/// </summary>
/// <remarks>
/// Os predicados abaixo são o único lugar das duas regras de UNI-REQ-0157: a gravação do
/// cronograma e das etapas e os gates de publicação perguntam aqui, e nenhum reescreve a regra.
/// </remarks>
/// <param name="EhResultado">Se o tipo é classificado como resultado.</param>
/// <param name="CongelaConfiguracao">Se publicar o tipo cria uma versão da configuração.</param>
public sealed record ClassificacaoDoTipoDeAto(bool EhResultado, bool CongelaConfiguracao)
{
    /// <summary>
    /// O tipo é resultado e o produto não declara papel. O resultado sem preliminar é declarado
    /// definitivo: só o ato que não é resultado fica sem papel.
    /// </summary>
    public DomainError? ViolacaoDoResultadoSemPapel(string atoCodigo, PapelProdutoFase? papel, string lugar) =>
        papel is null && EhResultado
            ? new DomainError(
                ProcessoSeletivoErrorCodes.ResultadoDeclaradoSemPapel,
                $"O tipo de ato '{atoCodigo}', declarado {lugar}, é resultado no catálogo e precisa de papel: o resultado sem preliminar é declarado definitivo.")
            : null;

    /// <summary>
    /// O tipo congela a configuração e o produto o declara como item de resultado. A correção de
    /// um resultado não cria versão nova, então um tipo que congela não teria correção possível.
    /// </summary>
    public DomainError? ViolacaoDoTipoQueCongelaComPapel(string atoCodigo, PapelProdutoFase? papel, string lugar) =>
        papel is not null && CongelaConfiguracao
            ? new DomainError(
                ProcessoSeletivoErrorCodes.TipoQueCongelaDeclaradoComoResultado,
                $"O tipo de ato '{atoCodigo}', declarado {lugar}, congela a configuração e não pode ser item de resultado: a correção de um resultado não cria versão nova.")
            : null;

    /// <summary>A primeira das duas violações, ou nulo quando o produto é coerente com o catálogo.</summary>
    public DomainError? ViolacaoDoProduto(string atoCodigo, PapelProdutoFase? papel, string lugar) =>
        ViolacaoDoResultadoSemPapel(atoCodigo, papel, lugar) ?? ViolacaoDoTipoQueCongelaComPapel(atoCodigo, papel, lugar);
}
