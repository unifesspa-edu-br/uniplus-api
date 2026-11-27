namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Campi;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class ObterCampusPorIdQueryHandler
{
    public static async Task<Result<CampusDto>> Handle(
        ObterCampusPorIdQuery query,
        ICampusRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        Campus? campus = await repository
            .ObterPorIdParaLeituraAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        return campus is null
            ? Result<CampusDto>.Failure(new DomainError(CampusErrorCodes.NaoEncontrado, "Campus não encontrado."))
            : Result<CampusDto>.Success(campus.ToDto());
    }
}
