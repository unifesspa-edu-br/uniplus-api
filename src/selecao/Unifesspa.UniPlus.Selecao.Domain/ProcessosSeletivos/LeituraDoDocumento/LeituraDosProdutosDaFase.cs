namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os atos que a fase publica. O ato que o catálogo de publicações não tem vigente no instante da leitura não entra, e a lista da
/// fase fica com parte recusada, que suspende o que se conclui do que a fase publica. Não confere o papel do ato.
/// </summary>
internal static class LeituraDosProdutosDaFase
{
    internal static ValorLido<IReadOnlyList<(ProdutoDaFase Item, int PosicaoNoDocumento)>> Ler(FaseNoDocumento fase, CadastrosCitadosNoDocumento cadastros)
    {
        ProdutoNoDocumento[] citados = [.. fase.Publicacoes.Produtos];

        return new(
            [.. citados.Where(produto => cadastros.TipoDeAtoVigente(produto.AtoCodigo)).Select(static produto => (ProdutoDaFase.Criar(produto.AtoCodigo, produto.Papel), produto.PosicaoNoDocumento))],
            [.. citados.Where(produto => !cadastros.TipoDeAtoVigente(produto.AtoCodigo)).Select(NaoEncontrado)]);
    }

    private static RecusaDaParte NaoEncontrado(ProdutoNoDocumento produto) => new(
        FaseDeclarada.ListaDosProdutos,
        $"produtos[{produto.PosicaoNoDocumento}].atoCodigo",
        produto.PosicaoNoDocumento,
        null,
        new DomainError(
            FaseCronogramaErrorCodes.AtoDoProdutoNaoEncontrado,
            "O tipo de ato publicado pela fase não tem versão vigente no catálogo de Publicações."));
}
