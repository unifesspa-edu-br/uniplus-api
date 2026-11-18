namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A regra de homologação que a fase declara, conferida contra os valores que a própria fase declara. Nada
/// aqui lê o código canônico da fase: a fase que homologa é a que declara a regra.
/// </summary>
public static class ConferenciaDaRegraDeHomologacao
{
    /// <summary>
    /// A regra precisa apontar o resultado da fase. A falta não depende dos produtos, e por isso é conferida
    /// mesmo quando a lista de produtos da fase tem recusa.
    /// </summary>
    public static FieldError? ResultadoAusente(string codigoDaFase, RegraDeHomologacaoDasInscricoes? regra)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoDaFase);

        return regra is { ResultadoAtoCodigo: null } ? RecusaDoResultadoAusente(codigoDaFase) : null;
    }

    /// <summary>
    /// O resultado que a regra aponta precisa ser produto, com papel, da própria fase. Depende da lista de produtos
    /// inteira: só se confere com produtos que a leitura do documento aceitou por inteiro, porque o produto recusado
    /// pode ser o resultado. O definitivo do resultado é conferido pelo ciclo recursal da fase, e não por esta regra.
    /// </summary>
    public static FieldError? ResultadoQueNaoEProduto(string codigoDaFase, PublicacoesDaFase publicacoes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoDaFase);
        ArgumentNullException.ThrowIfNull(publicacoes);

        return publicacoes.RegraDeHomologacao is { ResultadoAtoCodigo: { } resultado } && !ProdutoComPapel(publicacoes, resultado)
            ? RecusaDoResultadoQueNaoEProduto(codigoDaFase)
            : null;
    }

    private static bool ProdutoComPapel(PublicacoesDaFase publicacoes, string resultado) =>
        publicacoes.Produtos.Any(produto => produto.Papel is not null && string.Equals(produto.AtoCodigo, resultado, StringComparison.Ordinal));

    private static FieldError RecusaDoResultadoAusente(string codigoDaFase) => Recusa(
        $"A regra de homologação da fase '{codigoDaFase}' precisa apontar o resultado da fase que publica a lista.");

    private static FieldError RecusaDoResultadoQueNaoEProduto(string codigoDaFase) => Recusa(
        $"O resultado que a regra de homologação aponta precisa ser produto, com papel, da fase '{codigoDaFase}'.");

    private static FieldError Recusa(string mensagem) => new(
        $"{FaseDeclarada.CampoDaRegraDeHomologacao}.resultadoAtoCodigo",
        new DomainError(HomologacaoDasInscricoesErrorCodes.ResultadoNaoDeclarado, mensagem));
}
