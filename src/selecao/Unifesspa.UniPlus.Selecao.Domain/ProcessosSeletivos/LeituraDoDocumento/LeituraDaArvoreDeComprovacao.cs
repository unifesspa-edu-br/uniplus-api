namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Lê a árvore de comprovação em profundidade: cada nó carrega o caminho dele no documento, pelas posições enviadas, e a leitura do pai
/// vem antes das dos itens, de modo que as recusas saem na ordem do documento e cada uma no ponteiro do seu nó. A recusa de um nó não impede
/// a leitura dos demais, e o grupo só é adotado com todos os itens adotados. A profundidade já é limitada pelo documento. A ordem da regra é
/// a posição enviada mais 1, sem renumerar, e as folhas que citam a mesma chave usam o mesmo documento exigido.
/// </summary>
internal static class LeituraDaArvoreDeComprovacao
{
    internal static ArvoreLida Ler(IReadOnlyList<RegraDeComprovacaoNoDocumento> regras, DocumentosLidos documentos)
    {
        ArgumentNullException.ThrowIfNull(regras);
        ArgumentNullException.ThrowIfNull(documentos);

        return new([.. regras.Select(regra => LerNo(regra, $"{PartesRecusadasDasExigencias.ListaDasRegras}[{regra.PosicaoNoDocumento}]", documentos))]);
    }

    private static NoLido LerNo(RegraDeComprovacaoNoDocumento regra, string caminho, DocumentosLidos documentos)
    {
        NoLido[] itens = [.. regra.Itens.Select(item => LerNo(item, $"{caminho}.itens[{item.PosicaoNoDocumento}]", documentos))];
        RegraPropriaLida propria = LerPropria(regra, itens, documentos);

        return new(
            propria.Regra,
            regra.PosicaoNoDocumento,
            itens,
            [.. propria.Recusas.Select(recusa => recusa.NoCaminhoDoItem(caminho)), .. itens.SelectMany(static item => item.Recusas)]);
    }

    private static RegraPropriaLida LerPropria(RegraDeComprovacaoNoDocumento regra, NoLido[] itens, DocumentosLidos documentos) =>
        regra.Forma is FormaDeComprovacao.Documento or FormaDeComprovacao.TodosOsItens or FormaDeComprovacao.QuantidadeMinimaDeItens
            ? LeituraDaRegraDeComprovacao.Ler(regra, itens, documentos)
            : new(null, [.. DeclaracaoDaForma(regra)]);

    // A forma ausente ou desconhecida é recusa da regra, e a declaração a recusa com a causa da forma.
    private static IEnumerable<FieldError> DeclaracaoDaForma(RegraDeComprovacaoNoDocumento regra) =>
        Domain.ValueObjects.DeclaracaoDaRegraDeComprovacao.Interpretar(null, regra.ExigenciaChave is not null, regra.Itens.Count, regra.QuantidadeMinima).Recusas;
}
