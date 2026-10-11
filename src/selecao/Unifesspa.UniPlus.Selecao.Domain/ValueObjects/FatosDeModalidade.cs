namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os fatos cujos valores são códigos de modalidade: a modalidade de concorrência, o grupo em que o
/// candidato foi convocado e todo fato que o catálogo declare com essa fonte de valores.
/// </summary>
/// <remarks>
/// Chega pronto da Application, que o lê do catálogo: o agregado cita o fato por código e não
/// conhece a fonte dos seus valores. As proteções que dependem da modalidade ofertada — redefinir a
/// distribuição, a coerência da consequência de indeferimento com a ação da vaga e a cobertura legal
/// por modalidade — decidem por este conjunto, nunca por um código fixo. Pela mesma leitura vem o
/// fato de membro de cada agregado sobre grupo repetível, que a regra de uma modalidade ofertada pode
/// citar: o agregado do processo também não conhece o vínculo de agregação.
/// </remarks>
public sealed class FatosDeModalidade
{
    private readonly HashSet<string> _codigos;

    public FatosDeModalidade(IEnumerable<string> codigos, IReadOnlyDictionary<string, string> membroPorAgregado)
    {
        ArgumentNullException.ThrowIfNull(codigos);
        ArgumentNullException.ThrowIfNull(membroPorAgregado);
        _codigos = new HashSet<string>(codigos, StringComparer.Ordinal);
        MembroPorAgregado = new Dictionary<string, string>(membroPorAgregado, StringComparer.Ordinal);
    }

    public IReadOnlyCollection<string> Codigos => _codigos;

    /// <summary>O fato de membro de cada agregado sobre grupo repetível do catálogo, pelo código do agregado.</summary>
    public IReadOnlyDictionary<string, string> MembroPorAgregado { get; }

    public bool Contem(string fato) => _codigos.Contains(fato);
}
