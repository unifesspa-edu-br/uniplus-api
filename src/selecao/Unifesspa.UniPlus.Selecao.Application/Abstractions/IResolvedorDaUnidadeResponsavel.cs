namespace Unifesspa.UniPlus.Selecao.Application.Abstractions;

using Domain.ValueObjects;

/// <summary>
/// Resolve a unidade da Unifesspa que responde por uma fase do cronograma, a partir do cadastro
/// vivo da Organização Institucional (ADR-0056), na forma que o cronograma congela por
/// snapshot-copy (ADR-0061).
/// </summary>
/// <remarks>
/// Existe para que o handler do cronograma não dependa do <c>IUnidadeReader</c> da Organização
/// diretamente. O adaptador é interno à infraestrutura, e o opt-in de service location (ADR-0098)
/// fica no <c>SelecaoCodegenRegistration</c>.
/// </remarks>
public interface IResolvedorDaUnidadeResponsavel
{
    /// <summary>
    /// A unidade viva de <paramref name="unidadeId"/>, ou <see langword="null"/> quando ela não
    /// existe ou foi excluída do cadastro — que não tem outro estado de inativa.
    /// </summary>
    Task<UnidadeResponsavelSnapshot?> ResolverAsync(Guid unidadeId, CancellationToken cancellationToken = default);
}
