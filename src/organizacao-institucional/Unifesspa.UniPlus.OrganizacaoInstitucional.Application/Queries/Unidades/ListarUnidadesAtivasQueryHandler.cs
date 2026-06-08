namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.Unidades;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Mappings;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

public static class ListarUnidadesAtivasQueryHandler
{
    public static async Task<IReadOnlyList<UnidadeDto>> Handle(
        ListarUnidadesAtivasQuery query,
        IUnidadeRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        IReadOnlyList<Unidade> unidades = await repository
            .ListarAtivasAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. unidades.Select(u => u.ToDto())];
    }
}
