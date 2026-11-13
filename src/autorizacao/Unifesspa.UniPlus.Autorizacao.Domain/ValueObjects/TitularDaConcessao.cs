namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// A pessoa a quem a concessão se dá: a identidade do token e o e-mail institucional, que a revogação pede de volta para
/// confirmar a pessoa. A concessão normaliza o e-mail ao gravar.
/// </summary>
/// <param name="Pessoa">Emissor, <c>sub</c> e nome de exibição do beneficiário.</param>
/// <param name="Email">E-mail institucional do beneficiário; nulo quando a origem não o tinha.</param>
public sealed record TitularDaConcessao(Identidade Pessoa, string? Email)
{
    /// <summary>O beneficiário; nunca nulo.</summary>
    public Identidade Pessoa { get; } = Pessoa ?? throw new ArgumentNullException(nameof(Pessoa));
}
