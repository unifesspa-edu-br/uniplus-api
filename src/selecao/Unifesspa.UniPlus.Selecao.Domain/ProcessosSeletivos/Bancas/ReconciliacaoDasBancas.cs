namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

/// <summary>
/// Reaproveita a banca já gravada de mesmo id ao declarar ou reconstruir as bancas de uma fase ou de uma etapa: o id é o que
/// a designação, a atribuição e o julgamento apontam, e precisa sobreviver à troca de tipo, de regra e de posição. A banca
/// declarada sem id, ou com id que não é de banca do lugar, é banca nova. Cada banca gravada é consumida no máximo uma vez.
/// </summary>
internal static class ReconciliacaoDasBancas
{
    /// <summary>
    /// As bancas que o lugar passa a ter, na ordem das declaradas, cada uma com a posição no tipo que a ordem de chegada lhe
    /// dá. Materializa a lista antes de a coleção do lugar ser esvaziada.
    /// </summary>
    internal static List<TBanca> Reconciliar<TBanca>(IEnumerable<TBanca> gravadas, IEnumerable<TBanca> declaradas)
        where TBanca : class, IBancaDoLugar<TBanca>
    {
        List<TBanca> restantes = [.. gravadas];
        List<TBanca> chegadas = [.. declaradas];
        PosicaoDasBancasNoTipo.Numerar(chegadas);

        return [.. chegadas.Select(declarada => ReaproveitarOuAdotar(restantes, declarada))];
    }

    private static TBanca ReaproveitarOuAdotar<TBanca>(List<TBanca> restantes, TBanca declarada)
        where TBanca : class, IBancaDoLugar<TBanca>
    {
        TBanca? gravada = declarada.IdDaBancaQueSubstitui is { } id ? restantes.Find(banca => banca.Id == id) : null;
        if (gravada is null)
        {
            return declarada;
        }

        restantes.Remove(gravada);
        gravada.AssumirDeclaracao(declarada);

        return gravada;
    }
}
