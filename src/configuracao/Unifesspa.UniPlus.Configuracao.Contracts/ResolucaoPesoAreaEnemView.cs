namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Uma resolução de Pesos por Área como o processo seletivo a congela: as linhas vivas,
/// uma por grupo de área, e os grupos que ainda não têm linha. Quem sabe quais grupos
/// existem é a Configuração; o consumidor decide pela completude sem enumerá-los.
/// </summary>
/// <param name="Resolucao">A resolução, como gravada no cadastro.</param>
/// <param name="Linhas">As linhas vivas da resolução, ordenadas pelo código do grupo (ordinal).</param>
/// <param name="GruposAusentes">Os grupos de área sem linha viva nesta resolução, na ordem de exibição dos grupos.</param>
public sealed record ResolucaoPesoAreaEnemView(
    string Resolucao,
    IReadOnlyList<PesoAreaEnemView> Linhas,
    IReadOnlyList<GrupoAreaEnemView> GruposAusentes)
{
    /// <summary>A resolução tem linha viva para todos os grupos de área.</summary>
    public bool Completa => GruposAusentes.Count == 0;
}
