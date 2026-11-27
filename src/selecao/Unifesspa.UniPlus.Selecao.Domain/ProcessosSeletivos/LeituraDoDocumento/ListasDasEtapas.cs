namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Ordenacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A origem das listas de cada etapa adotada: os produtos, as bancas, os recursos e os procedimentos de cada recurso. A etapa guarda
/// só o que a leitura aceitou, em ordem canônica, e a causa de uma conferência que aponta o item pela posição dele na lista deve
/// achar a posição em que ele veio no documento, que o item recusado antes dele desloca. A lista dos itens de avaliação e a dos
/// níveis ficam na ordem do documento, que é a canônica, e não têm origem.
/// </summary>
internal static class ListasDasEtapas
{
    /// <summary>O nome da lista das etapas na declaração, pelo qual a origem e as partes recusadas a conhecem.</summary>
    internal const string ListaDasEtapas = "etapas";

    internal static IEnumerable<OrigemNoDocumento> Origens(IReadOnlyList<EtapaLida> lidas, OrigemNoDocumento posicoes)
    {
        Dictionary<int, int> canonicaPorOrigem = PartesRecusadasDasEtapas.PosicoesCanonicas(lidas, posicoes);

        return lidas
            .Where(static lida => lida.Etapa is not null)
            .SelectMany(lida => Listas($"{ListaDasEtapas}[{canonicaPorOrigem[lida.PosicaoNoDocumento]}]", lida.Etapa!, lida.Itens));
    }

    private static IEnumerable<OrigemNoDocumento> Listas(string etapa, EtapaProcesso adotada, ItensAdotadosDaEtapa itens) =>
        new[]
        {
            Lista(etapa, EtapaDeclarada.ListaDosProdutos, itens.Produtos, OrdemDaConfiguracao.ProdutosDaEtapa),
            Lista(etapa, EtapaDeclarada.ListaDasBancas, itens.Bancas, OrdemDaConfiguracao.BancasDaEtapa),
            Lista(etapa, EtapaDeclarada.ListaDosRecursos, [.. itens.Recursos.Select(static recurso => (recurso.Recurso, recurso.PosicaoNoDocumento))], OrdemDosRecursos(adotada)),
        }.Concat(Procedimentos(etapa, adotada, itens.Recursos)).OfType<OrigemNoDocumento>();

    private static IEnumerable<OrigemNoDocumento?> Procedimentos(string etapa, EtapaProcesso adotada, IReadOnlyList<RecursoLido> recursos)
    {
        List<RecursoDaEtapa> canonicos = [.. adotada.Recursos];

        return recursos.Select(recurso => Lista(
            $"{etapa}.{EtapaDeclarada.ListaDosRecursos}[{canonicos.FindIndex(canonico => ReferenceEquals(canonico, recurso.Recurso))}]",
            "procedimentos",
            recurso.Procedimentos,
            ProcedimentosDeRecurso.NaOrdemDaConfiguracao));
    }

    private static Func<IEnumerable<RecursoDaEtapa>, IOrderedEnumerable<RecursoDaEtapa>> OrdemDosRecursos(EtapaProcesso adotada) =>
        recursos => OrdemDaConfiguracao.RecursosDaEtapa(recursos, adotada.Produtos);

    private static OrigemNoDocumento? Lista<T>(
        string etapa,
        string lista,
        IReadOnlyList<(T Item, int PosicaoNoDocumento)> aceitos,
        Func<IEnumerable<T>, IOrderedEnumerable<T>> ordemCanonica)
        where T : class =>
        aceitos.Count == 0 ? null : OrigemNoDocumento.DaLista($"{etapa}.{lista}", aceitos, ordemCanonica, []);
}
