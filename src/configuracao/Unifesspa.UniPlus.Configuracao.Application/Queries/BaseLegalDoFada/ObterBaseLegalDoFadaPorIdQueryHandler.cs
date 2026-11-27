namespace Unifesspa.UniPlus.Configuracao.Application.Queries.BaseLegalDoFada;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class ObterBaseLegalDoFadaPorIdQueryHandler
{
    public static async Task<Result<BaseLegalDoFadaDto>> Handle(
        ObterBaseLegalDoFadaPorIdQuery query,
        IBaseLegalDoFadaRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        BaseLegalDoFada? entity = await repository.ObterPorIdParaLeituraAsync(query.Id, cancellationToken).ConfigureAwait(false);
        return entity is null
            ? Result<BaseLegalDoFadaDto>.Failure(new DomainError(BaseLegalDoFadaErrorCodes.NaoEncontrado, "Base legal do Fator de Atenuação das Desigualdades de Acesso não encontrada."))
            : Result<BaseLegalDoFadaDto>.Success(entity.ToDto());
    }
}
