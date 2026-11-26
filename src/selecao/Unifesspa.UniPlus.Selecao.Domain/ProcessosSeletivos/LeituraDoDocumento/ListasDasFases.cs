namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Ordenacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A origem das listas de cada fase adotada: os produtos, as bancas e os procedimentos de recurso. A fase guarda só o que a leitura
/// aceitou, em ordem canônica, e a causa de uma conferência que aponta o item pela posição dele na lista deve achar a posição em que
/// ele veio no documento, que o item recusado antes dele desloca.
/// </summary>
internal static class ListasDasFases
{
    internal static IEnumerable<OrigemNoDocumento> Origens(IReadOnlyList<FaseLida> lidas, OrigemNoDocumento posicoes)
    {
        Dictionary<int, int> canonicaPorOrigem = PartesRecusadasDasFases.PosicoesCanonicas(lidas, posicoes);

        return lidas
            .Where(static lida => lida.Fase is not null)
            .SelectMany(lida => Listas($"fases[{canonicaPorOrigem[lida.PosicaoNoDocumento]}]", lida.Itens));
    }

    private static IEnumerable<OrigemNoDocumento> Listas(string fase, ItensAdotadosDaFase itens) =>
        new[]
        {
            Lista(fase, FaseDeclarada.ListaDosProdutos, itens.Produtos, OrdemDaConfiguracao.ProdutosDaFase),
            Lista(fase, FaseDeclarada.ListaDasBancasRequeridas, itens.Bancas, static bancas => OrdemDaConfiguracao.BancasDaFase(bancas)),
            Lista(fase, FaseDeclarada.ListaDosProcedimentosDeRecurso, itens.Procedimentos, ProcedimentosDeRecurso.NaOrdemDaConfiguracao),
        }.OfType<OrigemNoDocumento>();

    private static OrigemNoDocumento? Lista<T>(
        string fase,
        string lista,
        IReadOnlyList<(T Item, int PosicaoNoDocumento)> aceitos,
        Func<IEnumerable<T>, IOrderedEnumerable<T>> ordemCanonica)
        where T : class =>
        aceitos.Count == 0 ? null : OrigemNoDocumento.DaLista($"{fase}.{lista}", aceitos, ordemCanonica, []);
}
