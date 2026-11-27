namespace Unifesspa.UniPlus.Configuracao.Application.Queries.FatosCandidato;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class ObterFatoCandidatoParaManutencaoQueryHandler
{
    public static async Task<Result<FatoDeManutencaoDto>> Handle(
        ObterFatoCandidatoParaManutencaoQuery query,
        IFatoCandidatoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        FatoCandidato? fato = await repository.ObterPorIdParaLeituraAsync(query.Id, cancellationToken).ConfigureAwait(false);
        return fato is null
            ? Result<FatoDeManutencaoDto>.Failure(new DomainError(FatoCandidatoErrorCodes.NaoEncontrado, "Dado do candidato não encontrado."))
            : Result<FatoDeManutencaoDto>.Success(fato.ToDto());
    }
}
