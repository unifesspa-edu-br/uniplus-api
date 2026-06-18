namespace Unifesspa.UniPlus.Geo.Application.Queries.Cep;

using Unifesspa.UniPlus.Geo.Application.Abstractions;
using Unifesspa.UniPlus.Geo.Application.DTOs;

/// <summary>
/// Handler convention-based de <see cref="ResolverCepQuery"/>: delega ao
/// <see cref="ICepResolver"/> (cache-aside + cascata) e devolve o
/// <c>CepResolvidoDto</c> ou <see langword="null"/> (404 no controller).
/// </summary>
public static class ResolverCepQueryHandler
{
    public static async Task<CepResolvidoDto?> Handle(
        ResolverCepQuery query,
        ICepResolver resolver,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(resolver);

        return await resolver.ResolverAsync(query.Cep, cancellationToken).ConfigureAwait(false);
    }
}
