namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os atos que a etapa publica. O ato que o catálogo de publicações não tem vigente no instante da leitura não entra, e a lista da
/// etapa fica com parte recusada, que suspende o que se conclui do que a etapa publica. Não confere o papel do ato nem a repetição
/// do par ato e papel, que são da declaração da etapa.
/// </summary>
internal static class LeituraDosProdutosDaEtapa
{
    internal static ValorLido<IReadOnlyList<(ProdutoDaEtapa Item, int PosicaoNoDocumento)>> Ler(EtapaNoDocumento etapa, CadastrosCitadosNoDocumento cadastros)
    {
        ArgumentNullException.ThrowIfNull(etapa);
        ArgumentNullException.ThrowIfNull(cadastros);

        return new(
            [.. etapa.PublicacoesPrevistas.Where(produto => cadastros.TipoDeAtoVigente(produto.AtoCodigo)).Select(static produto => (ProdutoDaEtapa.Criar(produto.AtoCodigo, produto.Papel), produto.PosicaoNoDocumento))],
            [.. etapa.PublicacoesPrevistas.Where(produto => !cadastros.TipoDeAtoVigente(produto.AtoCodigo)).Select(NaoEncontrado)]);
    }

    private static RecusaDaParte NaoEncontrado(ProdutoNoDocumento produto) => new(
        EtapaDeclarada.ListaDosProdutos,
        $"{EtapaDeclarada.ListaDosProdutos}[{produto.PosicaoNoDocumento}].atoCodigo",
        produto.PosicaoNoDocumento,
        null,
        new DomainError(
            EtapaProcessoErrorCodes.AtoDoProdutoNaoEncontrado,
            "O tipo de ato publicado pela etapa não tem versão vigente no catálogo de Publicações."));
}
