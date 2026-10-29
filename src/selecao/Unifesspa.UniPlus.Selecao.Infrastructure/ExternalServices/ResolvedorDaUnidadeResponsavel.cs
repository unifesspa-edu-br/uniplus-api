namespace Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Implementação de <see cref="IResolvedorDaUnidadeResponsavel"/> sobre o
/// <see cref="IUnidadeReader"/> da Organização Institucional (ADR-0056).
/// </summary>
internal sealed class ResolvedorDaUnidadeResponsavel : IResolvedorDaUnidadeResponsavel
{
    private readonly IUnidadeReader _unidadeReader;

    public ResolvedorDaUnidadeResponsavel(IUnidadeReader unidadeReader)
    {
        ArgumentNullException.ThrowIfNull(unidadeReader);
        _unidadeReader = unidadeReader;
    }

    public async Task<UnidadeResponsavelSnapshot?> ResolverAsync(Guid unidadeId, CancellationToken cancellationToken = default)
    {
        UnidadeView? unidade = await _unidadeReader
            .ObterPorIdAsync(unidadeId, cancellationToken)
            .ConfigureAwait(false);

        return unidade is null ? null : new UnidadeResponsavelSnapshot(unidade.Id, unidade.Sigla, unidade.Nome);
    }
}
