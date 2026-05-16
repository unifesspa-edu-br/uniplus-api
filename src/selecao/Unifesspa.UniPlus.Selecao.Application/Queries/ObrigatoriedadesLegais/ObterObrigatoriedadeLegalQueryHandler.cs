namespace Unifesspa.UniPlus.Selecao.Application.Queries.ObrigatoriedadesLegais;

using System.Collections.Generic;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Handler convention-based de <see cref="ObterObrigatoriedadeLegalQuery"/>.
/// Carrega a regra (filtrada por soft-delete via query filter padrão) e
/// hidrata <c>AreasDeInteresse</c> a partir da junction temporal — entity
/// não tem nav property (ADR-0060), o set in-memory vem vazio após carga
/// EF fresca.
/// </summary>
public static class ObterObrigatoriedadeLegalQueryHandler
{
    public static async Task<ObrigatoriedadeLegalDto?> Handle(
        ObterObrigatoriedadeLegalQuery query,
        IObrigatoriedadeLegalRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        ObrigatoriedadeLegal? regra = await repository
            .ObterPorIdAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);
        if (regra is null)
        {
            return null;
        }

        IReadOnlySet<AreaCodigo> areas = await repository
            .ObterAreasVigentesAsync(regra.Id, cancellationToken)
            .ConfigureAwait(false);

        return ObrigatoriedadeLegalMapping.ToDto(regra, areas);
    }
}
