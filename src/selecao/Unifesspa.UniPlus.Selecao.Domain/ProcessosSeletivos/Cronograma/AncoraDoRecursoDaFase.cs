namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// A âncora do prazo de recurso da fase: o produto preliminar de que o prazo conta, a conferência de
/// que ela aponta para um produto da própria fase e o reapontamento dela quando os produtos da fase
/// são recriados ou reconciliados.
/// </summary>
/// <remarks>
/// Recurso é contra o resultado preliminar: é ele que abre a janela de interposição, e é do instante em
/// que é publicado que o prazo corre (UNI-REQ-0115). Fase que não publica nenhum não tem decisão a
/// contestar, nem a que só publica o definitivo, que encerra a matéria em vez de abri-la. E a âncora é
/// a identidade de um produto DESTA fase, não um código de tipo de ato: duas fases podem publicar o
/// mesmo tipo, e só a identidade da linha diz de qual das duas publicações o prazo conta.
/// </remarks>
public static class AncoraDoRecursoDaFase
{
    /// <summary>
    /// A violação da âncora do prazo de recurso, ou <see langword="null"/> quando a fase não admite
    /// recurso ou o ancora corretamente.
    /// </summary>
    /// <remarks>
    /// <b>Predicado único, quatro chamadores.</b> <see cref="FaseCronograma.Criar"/> o acumula como
    /// recusa, <see cref="FaseCronograma.Reidratar"/> o lança como erro de programação, o decodificador
    /// do envelope o propaga como recusa nomeada e <c>ProcessoSeletivo</c> o projeta no gate de
    /// publicação e no item de conformidade. Escrito uma vez, nenhum deles pode divergir dos outros: foi
    /// exatamente assim que a conferência da reidratação passou a valer meia invariante.
    /// </remarks>
    public static FieldError? Violacao(
        string codigoDaFase,
        IReadOnlyList<ProdutoDaFase> produtos,
        RegraRecursoFase? regraRecurso)
    {
        if (regraRecurso is null)
        {
            return null;
        }

        List<ProdutoDaFase> preliminares =
            [.. produtos.Where(static p => p.Papel == PapelProdutoFase.Preliminar)];

        return preliminares.Count == 0
            ? FaseSemProdutoPreliminar(codigoDaFase)
            : AncoraForaDosPreliminares(codigoDaFase, preliminares, regraRecurso);
    }

    /// <summary>
    /// Reaponta a âncora da regra do produto de origem para o produto equivalente de destino, quando os
    /// produtos da fase trocaram de instância e o Id para onde a âncora aponta deixou de ser o que
    /// permanece. Sem isso a regra de recurso ficaria referenciando uma linha que a mesma transação
    /// remove como órfã, e o sintoma só apareceria ao resolver a configuração de recurso de um ato
    /// publicado.
    /// </summary>
    /// <remarks>
    /// A restauração da configuração congelada passa por dois remapeamentos sucessivos: do produto
    /// congelado para o recriado e do recriado para o rastreado que sobrevive à reconciliação. A ponte é
    /// o par ato e papel, porque é a chave da própria reconciliação e é único dentro da fase: o ato
    /// sozinho não é, desde que a mesma matéria passou a ser publicável como preliminar e como
    /// definitiva. Quando os produtos de origem não contêm a âncora, ou os de destino não têm o par, não
    /// há para onde remapear e a âncora fica como está: é estado que a criação da fase não deixa existir.
    /// </remarks>
    internal static void Reancorar(
        RegraRecursoFase regraRecurso,
        IEnumerable<ProdutoDaFase> produtosDeOrigem,
        IEnumerable<ProdutoDaFase> produtosDeDestino)
    {
        ProdutoDaFase? ancoraDeOrigem = produtosDeOrigem.FirstOrDefault(p => p.Id == regraRecurso.ProdutoAncoraId);
        if (ancoraDeOrigem is null)
        {
            return;
        }

        ProdutoDaFase? ancoraDeDestino = produtosDeDestino.FirstOrDefault(
            p => string.Equals(p.AtoCodigo, ancoraDeOrigem.AtoCodigo, StringComparison.Ordinal)
                && p.Papel == ancoraDeOrigem.Papel);
        if (ancoraDeDestino is not null && ancoraDeDestino.Id != regraRecurso.ProdutoAncoraId)
        {
            regraRecurso.RemapearAncora(ancoraDeDestino.Id);
        }
    }

    private static FieldError FaseSemProdutoPreliminar(string codigoDaFase) =>
        new("produtos", new DomainError(
            "RegraRecursoFase.FaseSemProdutoPreliminar",
            $"A fase '{codigoDaFase}' admite recurso e não publica nenhum produto com papel preliminar — declare o resultado preliminar em que o prazo ancora."));

    private static FieldError? AncoraForaDosPreliminares(
        string codigoDaFase,
        List<ProdutoDaFase> preliminares,
        RegraRecursoFase regraRecurso)
    {
        if (preliminares.Exists(p => p.Id == regraRecurso.ProdutoAncoraId))
        {
            return null;
        }

        return new("regraRecurso.atoAncoraCodigo", new DomainError(
            "RegraRecursoFase.AncoraNaoEhProdutoPreliminarDaFase",
            $"A regra de recurso da fase '{codigoDaFase}' não ancora em nenhum dos produtos preliminares que ela publica ({string.Join(", ", preliminares.Select(static p => p.AtoCodigo).Order(StringComparer.Ordinal))})."));
    }
}
