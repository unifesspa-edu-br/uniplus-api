namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.AreasOrganizacionais;

using DTOs;
using Mappings;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

public static class ListarAreasOrganizacionaisAtivasQueryHandler
{
    public static async Task<IReadOnlyList<AreaOrganizacionalDto>> Handle(
        ListarAreasOrganizacionaisAtivasQuery query,
        IAreaOrganizacionalRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        IReadOnlyList<AreaOrganizacional> areas = await repository
            .ListarAtivasAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. areas.Select(a => a.ToDto())];
    }
}
