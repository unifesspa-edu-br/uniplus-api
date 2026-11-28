namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O caminho em que cada exigência aparece, para a recusa apontar o campo certo. Na gravação é o do pedido, com a
/// posição do item em cada lista que o caminho atravessa; no documento da configuração a exigência é de uma lista
/// única, e o caminho é a posição nela.
/// </summary>
public static class CaminhosDasExigencias
{
    /// <summary>
    /// O caminho de cada regra no pedido, na ordem de <see cref="RegraDeComprovacao.EnumerarRegraEItens"/>: a regra na
    /// posição dela na <paramref name="lista"/>, e cada item de grupo dentro de <c>itens[]</c> da regra que o contém.
    /// </summary>
    public static IReadOnlyList<string> DasRegras(IReadOnlyList<RegraDeComprovacao> regrasSemGrupo, string lista)
    {
        ArgumentNullException.ThrowIfNull(regrasSemGrupo);
        ArgumentNullException.ThrowIfNull(lista);

        return [.. regrasSemGrupo.SelectMany((regra, posicao) => DaRegraEDosItens(regra, NaPosicao(lista, posicao)))];
    }

    /// <summary>
    /// O caminho do documento de cada regra de forma documento, na ordem de <paramref name="regras"/>;
    /// <paramref name="caminhosDasRegras"/> traz o caminho de cada uma delas.
    /// </summary>
    public static IReadOnlyList<string> DosDocumentos(IReadOnlyList<RegraDeComprovacao> regras, IReadOnlyList<string> caminhosDasRegras)
    {
        ArgumentNullException.ThrowIfNull(regras);
        ArgumentNullException.ThrowIfNull(caminhosDasRegras);
        ExigirUmParaCada(caminhosDasRegras, regras.Count, nameof(caminhosDasRegras));

        return [.. regras
            .Select((regra, posicao) => (regra, posicao))
            .Where(static par => par.regra.Forma == FormaDeComprovacao.Documento)
            .Select(par => DoDocumento(caminhosDasRegras[par.posicao]))];
    }

    /// <summary>O caminho do documento de uma regra de documento, dado o caminho da regra.</summary>
    public static string DoDocumento(string caminhoDaRegra)
    {
        ArgumentNullException.ThrowIfNull(caminhoDaRegra);
        return caminhoDaRegra + ".documento";
    }

    /// <summary>O caminho de cada uma das <paramref name="quantidade"/> exigências da lista única de nome <paramref name="lista"/>.</summary>
    public static IReadOnlyList<string> NaLista(string lista, int quantidade)
    {
        ArgumentNullException.ThrowIfNull(lista);
        return [.. Enumerable.Range(0, quantidade).Select(posicao => NaPosicao(lista, posicao))];
    }

    /// <summary>Recusa a lista de caminhos que não tem um caminho para cada exigência: o item apontaria o lugar errado.</summary>
    internal static void ExigirUmParaCada(IReadOnlyList<string> caminhos, int quantidadeDeExigencias, string parametro)
    {
        if (caminhos.Count != quantidadeDeExigencias)
        {
            throw new ArgumentException("Cada exigência precisa de um caminho, na mesma ordem.", parametro);
        }
    }

    private static string NaPosicao(string lista, int posicao) => $"{lista}[{posicao}]";

    private static IEnumerable<string> DaRegraEDosItens(RegraDeComprovacao regra, string caminho) =>
        [caminho, .. regra.Itens.SelectMany((item, posicao) => DaRegraEDosItens(item, NaPosicao(caminho + ".itens", posicao)))];
}
