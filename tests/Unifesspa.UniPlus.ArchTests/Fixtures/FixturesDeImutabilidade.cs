namespace Unifesspa.UniPlus.ArchTests.Fixtures;

/// <summary>
/// Contraprova da regra de imutabilidade do valor: um tipo com propriedade <c>init</c> e outro com propriedade
/// <c>set</c> pública têm de ser reprovados, e o de propriedade só de leitura, aprovado.
/// </summary>
internal static class FixturesDeImutabilidade
{
    internal sealed class ComPropriedadeInit
    {
        public string Valor { get; init; } = string.Empty;
    }

    internal sealed class ComPropriedadeSet
    {
        public string Valor { get; set; } = string.Empty;
    }

    internal sealed class SomenteLeitura
    {
        public string Valor { get; } = string.Empty;
    }
}
