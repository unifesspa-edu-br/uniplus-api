namespace Unifesspa.UniPlus.Configuracao.Application.Queries.FasesCanonicas;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class ObterFaseCanonicaPorIdQueryHandler
{
    public static async Task<Result<FaseCanonicaDto>> Handle(
        ObterFaseCanonicaPorIdQuery query,
        IFaseCanonicaRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        FaseCanonica? fase = await repository
            .ObterPorIdParaLeituraAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        return fase is null
            ? Result<FaseCanonicaDto>.Failure(new DomainError(FaseCanonicaErrorCodes.NaoEncontrada, "Fase canônica não encontrada."))
            : Result<FaseCanonicaDto>.Success(fase.ToDto());
    }
}
