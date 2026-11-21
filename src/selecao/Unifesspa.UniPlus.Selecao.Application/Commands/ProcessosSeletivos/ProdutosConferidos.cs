namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Kernel.Results;

/// <summary>
/// Os produtos de um dono (fase ou etapa) que passaram na conferência e as recusas dos demais. As recusas
/// trazem o caminho relativo ao dono, como <c>produtos[posicao].papel</c>.
/// </summary>
internal sealed record ProdutosConferidos<TProduto>(List<TProduto> Produtos, List<FieldError> Recusas)
{
    /// <summary>
    /// Sem todos os produtos íntegros não há o que a fábrica do dono avaliar: chamá-la com a coleção
    /// incompleta reportaria violações inventadas pela própria falha anterior.
    /// </summary>
    public bool Integros => Recusas.Count == 0;
}
