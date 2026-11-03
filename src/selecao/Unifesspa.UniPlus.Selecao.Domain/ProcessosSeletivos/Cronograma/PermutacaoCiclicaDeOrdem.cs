namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Mesmo casando as fases pela identidade estável, uma permutação cíclica de <c>Ordem</c> entre fases
/// retidas pede que linhas troquem valores cobertos pelo índice único de ordem entre si, e nenhuma
/// ordem de UPDATE resolve um ciclo fechado num único SaveChanges: o EF interrompe antes de emitir
/// SQL, por dependência circular. Uma cadeia aberta (que termina numa ordem livre ou na de uma fase
/// removida, que libera a linha por DELETE) ele resolve; só o ciclo fechado não tem ordenação.
/// </summary>
internal static class PermutacaoCiclicaDeOrdem
{
    private const int NaCaminhadaAtual = 1;
    private const int ConcluidoSemCiclo = 2;

    internal static DomainError? Recusa(
        Dictionary<Guid, FaseCronograma> fasesAntigasPorOrigem,
        Dictionary<Guid, FaseCronograma> fasesNovasPorOrigem)
    {
        Dictionary<int, Guid> origemAntigaPorOrdem = [];
        foreach (FaseCronograma antiga in fasesAntigasPorOrigem.Values.Where(f => fasesNovasPorOrigem.ContainsKey(f.FaseCanonicaOrigemId)))
        {
            origemAntigaPorOrdem[antiga.Ordem] = antiga.FaseCanonicaOrigemId;
        }

        Dictionary<Guid, int> estadoDoNo = [];
        foreach (Guid origemInicial in fasesAntigasPorOrigem.Keys)
        {
            if (!estadoDoNo.ContainsKey(origemInicial)
                && CaminhadaFechaCiclo(origemInicial, fasesAntigasPorOrigem, fasesNovasPorOrigem, origemAntigaPorOrdem, estadoDoNo))
            {
                return new DomainError(
                    "FaseCronograma.PermutacaoDeOrdemNaoSuportada",
                    "A redefinição do cronograma troca a Ordem entre fases já existentes formando um ciclo fechado (ex.: uma fase assume a Ordem de outra, que assume a Ordem da primeira) — isso não pode ser persistido em uma única chamada. Mova uma das fases para uma Ordem livre numa chamada separada antes de fechar o ciclo.");
            }
        }

        return null;
    }

    private static bool CaminhadaFechaCiclo(
        Guid origemInicial,
        Dictionary<Guid, FaseCronograma> fasesAntigasPorOrigem,
        Dictionary<Guid, FaseCronograma> fasesNovasPorOrigem,
        Dictionary<int, Guid> origemAntigaPorOrdem,
        Dictionary<Guid, int> estadoDoNo)
    {
        List<Guid> caminho = [];
        Guid? noAtual = origemInicial;
        bool cicloFechado = false;

        while (noAtual is { } no)
        {
            if (estadoDoNo.TryGetValue(no, out int estado))
            {
                cicloFechado = estado == NaCaminhadaAtual;
                break;
            }

            if (!fasesNovasPorOrigem.TryGetValue(no, out FaseCronograma? nova)
                || nova.Ordem == fasesAntigasPorOrigem[no].Ordem)
            {
                break;
            }

            estadoDoNo[no] = NaCaminhadaAtual;
            caminho.Add(no);
            noAtual = origemAntigaPorOrdem.TryGetValue(nova.Ordem, out Guid proximo) ? proximo : null;
        }

        foreach (Guid visitado in caminho)
        {
            estadoDoNo[visitado] = ConcluidoSemCiclo;
        }

        return cicloFechado;
    }
}
