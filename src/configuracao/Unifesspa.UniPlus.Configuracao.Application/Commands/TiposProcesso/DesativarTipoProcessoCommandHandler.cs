namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposProcesso;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <remarks>
/// Sem catch de conflito de concorrência de propósito: o endpoint é um DELETE sem
/// <c>[RequiresIdempotencyKey]</c>, e pela ADR-0119 esse é justamente o caso em que a
/// <c>DbUpdateConcurrencyException</c> do xmin propaga e o <c>GlobalExceptionMiddleware</c>
/// a mapeia centralmente para 409.
/// </remarks>
public static class DesativarTipoProcessoCommandHandler
{
    public static async Task<Result> Handle(
        DesativarTipoProcessoCommand command,
        ITipoProcessoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        TipoProcesso? tipo = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (tipo is null)
        {
            return Result.Failure(new DomainError(TipoProcessoErrorCodes.NaoEncontrado, "Tipo de processo seletivo não encontrado."));
        }

        Result desativar = tipo.Desativar();
        if (desativar.IsFailure)
        {
            return desativar;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
