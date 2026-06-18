namespace Unifesspa.UniPlus.Geo.Application.Queries.Estados;

using Unifesspa.UniPlus.Geo.Application.Abstractions;
using Unifesspa.UniPlus.Geo.Application.DTOs;
using Unifesspa.UniPlus.Geo.Application.Mappings;
using Unifesspa.UniPlus.Geo.Domain.Entities;

/// <summary>
/// Handler convention-based de <see cref="ObterEstadoPorUfQuery"/>: projeção
/// <c>AsNoTracking</c> pela chave natural <c>uf</c>; <see langword="null"/> quando
/// o Estado vigente inexiste.
/// </summary>
public static class ObterEstadoPorUfQueryHandler
{
    public static async Task<EstadoDto?> Handle(
        ObterEstadoPorUfQuery query,
        IEstadoReader reader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(reader);

        Estado? estado = await reader
            .ObterPorUfAsync(query.Uf, cancellationToken)
            .ConfigureAwait(false);

        return estado?.ToDto();
    }
}
