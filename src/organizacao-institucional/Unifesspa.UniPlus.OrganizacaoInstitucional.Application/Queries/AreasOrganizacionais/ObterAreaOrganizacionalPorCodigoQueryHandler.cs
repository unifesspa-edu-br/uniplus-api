namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.AreasOrganizacionais;

using DTOs;
using Mappings;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

public static class ObterAreaOrganizacionalPorCodigoQueryHandler
{
    public static async Task<AreaOrganizacionalDto?> Handle(
        ObterAreaOrganizacionalPorCodigoQuery query,
        IAreaOrganizacionalRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        Result<AreaCodigo> codigoResult = AreaCodigo.From(query.Codigo);
        if (codigoResult.IsFailure)
        {
            // Código inválido tem mesmo efeito user-facing que "não encontrado"
            // em endpoint público de leitura — 404. Mantém a uniformidade de
            // GET /resource/{id} (404 cobre tanto inexistente quanto malformado).
            return null;
        }

        AreaOrganizacional? area = await repository
            .ObterPorCodigoAsync(codigoResult.Value!, cancellationToken)
            .ConfigureAwait(false);

        return area?.ToDto();
    }
}
