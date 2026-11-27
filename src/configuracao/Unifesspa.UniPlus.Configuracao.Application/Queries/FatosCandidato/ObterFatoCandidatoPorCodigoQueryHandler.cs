namespace Unifesspa.UniPlus.Configuracao.Application.Queries.FatosCandidato;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

public static class ObterFatoCandidatoPorCodigoQueryHandler
{
    public static async Task<Result<FatoDoCatalogoDto>> Handle(
        ObterFatoCandidatoPorCodigoQuery query,
        IFatoCandidatoDoCatalogoReader reader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(reader);

        FatoDoCatalogoDto? fato = await reader
            .ObterPorCodigoAsync(query.Codigo, cancellationToken)
            .ConfigureAwait(false);

        return fato is null
            ? Result<FatoDoCatalogoDto>.Failure(new DomainError(FatoCandidatoErrorCodes.NaoEncontrado, "Dado do candidato não encontrado."))
            : Result<FatoDoCatalogoDto>.Success(fato);
    }
}
