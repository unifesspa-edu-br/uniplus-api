namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Reaproveita a linha viva de mesmo id ao declarar ou repor os itens de avaliação: o id é o que o
/// lançamento e o recurso apontam, e precisa sobreviver à troca de rótulo e pontuação. Materializa a
/// lista antes de a coleção da etapa ser esvaziada, para os ids novos nascerem na mesma ordem.
/// </summary>
internal static class ReconciliacaoDosItensDeAvaliacao
{
    internal static List<ItemDeAvaliacao> Reconciliar(
        IEnumerable<ItemDeAvaliacao> vivos, IReadOnlyList<ItemDeAvaliacaoDeclarado>? declarados)
    {
        List<ItemDeAvaliacao> restantes = [.. vivos];

        return [.. (declarados ?? []).Select(declarado =>
            ReaproveitarPeloId(restantes, declarado.Id, declarado.Rotulo, declarado.PontuacaoMaxima)
                ?? ItemDeAvaliacao.Criar(declarado))];
    }

    internal static List<ItemDeAvaliacao> ReporCongelados(
        IEnumerable<ItemDeAvaliacao> vivos, IEnumerable<ItemDeAvaliacao> congelados)
    {
        List<ItemDeAvaliacao> restantes = [.. vivos];

        return [.. congelados.Select(congelado =>
            ReaproveitarPeloId(restantes, congelado.Id, congelado.Rotulo, congelado.PontuacaoMaxima) ?? congelado)];
    }

    private static ItemDeAvaliacao? ReaproveitarPeloId(
        List<ItemDeAvaliacao> restantes, Guid? id, string rotulo, decimal pontuacaoMaxima)
    {
        ItemDeAvaliacao? existente = id is { } procurado ? restantes.Find(i => i.Id == procurado) : null;
        if (existente is null)
        {
            return null;
        }

        restantes.Remove(existente);
        existente.Atualizar(rotulo, pontuacaoMaxima);

        return existente;
    }
}
