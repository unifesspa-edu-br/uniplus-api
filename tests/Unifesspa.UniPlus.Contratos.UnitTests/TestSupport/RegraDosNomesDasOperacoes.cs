namespace Unifesspa.UniPlus.Contratos.UnitTests.TestSupport;

/// <summary>
/// A regra do nome de operação do contrato: toda operação tem nome, e o nome é único em todo o executável. O mesmo
/// endpoint publicado em mais de um documento do executável conta uma vez só.
/// </summary>
internal static class RegraDosNomesDasOperacoes
{
    public static IReadOnlyList<string> SemNome(IEnumerable<OperacaoPublicada> operacoes) =>
        [.. operacoes.Where(static operacao => string.IsNullOrWhiteSpace(operacao.Nome))
            .Select(static operacao => operacao.Descricao).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    public static IReadOnlyList<string> Repetidos(IEnumerable<OperacaoPublicada> operacoes) =>
        [.. operacoes.Where(static operacao => !string.IsNullOrWhiteSpace(operacao.Nome))
            .GroupBy(static operacao => (operacao.Executavel, operacao.Nome), static operacao => operacao.Identidade)
            .Select(static grupo => (grupo.Key.Nome, Operacoes: grupo.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()))
            .Where(static grupo => grupo.Operacoes.Length > 1)
            .Select(static grupo => $"{grupo.Nome}: {string.Join("; ", grupo.Operacoes)}")
            .Order(StringComparer.Ordinal)];
}
