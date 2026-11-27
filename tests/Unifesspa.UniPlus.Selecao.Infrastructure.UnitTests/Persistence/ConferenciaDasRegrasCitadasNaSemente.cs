namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

/// <summary>
/// Confere, por código e versão exatos, que cada regra citada por uma configuração congelada continua
/// existindo na semente do catálogo (ADR-0112). Código que só começa igual a outro é outra regra.
/// </summary>
internal static class ConferenciaDasRegrasCitadasNaSemente
{
    internal static IReadOnlyList<(string Codigo, string Versao)> Ausentes(
        IEnumerable<(string Codigo, string Versao)> citadas,
        IEnumerable<(string Codigo, string Versao)> semente)
    {
        ArgumentNullException.ThrowIfNull(citadas);
        ArgumentNullException.ThrowIfNull(semente);

        HashSet<(string Codigo, string Versao)> existentes = [.. semente];
        return [.. citadas.Distinct().Where(par => !existentes.Contains(par))
            .OrderBy(static par => par.Codigo, StringComparer.Ordinal)
            .ThenBy(static par => par.Versao, StringComparer.Ordinal)];
    }

    internal static string Descrever(IReadOnlyList<(string Codigo, string Versao)> ausentes) =>
        ausentes.Count == 0
            ? "Todas as regras citadas existem na semente do catálogo."
            : "Regras citadas por configuração congelada que faltam na semente do catálogo: "
                + string.Join("; ", ausentes.Select(static par => $"código '{par.Codigo}', versão '{par.Versao}'"))
                + ". A mudança de uma regra já citada entra como versão nova (ADR-0112).";
}
