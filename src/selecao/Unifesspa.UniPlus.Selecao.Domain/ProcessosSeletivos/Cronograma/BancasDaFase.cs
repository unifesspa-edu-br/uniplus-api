namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

/// <summary>As bancas que a fase requer, como valores: a cópia do tipo e a regra de deliberação de cada uma, na posição declarada.</summary>
public sealed record BancasDaFase
{
    internal BancasDaFase(IReadOnlyList<BancaRequerida> bancas) =>
        Declaradas = BancaDeclarada.De([.. bancas.Select(static banca => (banca.Tipo, banca.RegraDeDeliberacao))]);

    /// <summary>Se alguma banca da fase tem papel que julga recurso.</summary>
    public bool TemBancaDeRecursos => Declaradas.Any(static banca => banca.Tipo.JulgaRecurso);

    public IReadOnlyList<BancaDeclarada> Declaradas { get; }
}
