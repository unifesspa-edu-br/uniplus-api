namespace Unifesspa.UniPlus.Configuracao.Application.Queries.PrecedenciasFase;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class ObterPrecedenciaFasePorIdQueryHandler
{
    private static readonly DomainError NaoEncontrado = new(
        PrecedenciaFaseErrorCodes.NaoEncontrada,
        "Aresta de precedência não encontrada.");

    public static async Task<Result<PrecedenciaFaseDto>> Handle(
        ObterPrecedenciaFasePorIdQuery query,
        IPrecedenciaFaseRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        PrecedenciaFase? aresta = await repository
            .ObterPorIdParaLeituraAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        return aresta is null
            ? Result<PrecedenciaFaseDto>.Failure(NaoEncontrado)
            : Result<PrecedenciaFaseDto>.Success(aresta.ToDto());
    }
}
