namespace Unifesspa.UniPlus.Configuracao.Application.Queries.PesosAreaEnem;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class ObterPesoAreaEnemPorIdQueryHandler
{
    private static readonly DomainError NaoEncontrado = new(
        PesoAreaEnemErrorCodes.NaoEncontrado,
        "Linha de pesos do ENEM não encontrada.");

    public static async Task<Result<PesoAreaEnemDto>> Handle(
        ObterPesoAreaEnemPorIdQuery query,
        IPesoAreaEnemRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        PesoAreaEnem? peso = await repository
            .ObterPorIdParaLeituraAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        return peso is null
            ? Result<PesoAreaEnemDto>.Failure(NaoEncontrado)
            : Result<PesoAreaEnemDto>.Success(peso.ToDto());
    }
}
