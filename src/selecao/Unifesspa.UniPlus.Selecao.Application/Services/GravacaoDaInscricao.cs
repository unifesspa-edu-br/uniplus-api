namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Abstractions;

using Commands;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// Grava a inscrição alterada pelo candidato. O endpoint tem <c>Idempotency-Key</c> (ADR-0119): a concorrência é capturada aqui e as
/// alterações não salvas são descartadas, para o <c>SaveChangesAsync</c> do outbox não reencontrar a entidade modificada.
/// </summary>
public sealed class GravacaoDaInscricao(IInscricaoRepository inscricoes, IUnitOfWork unitOfWork)
{
    public async Task<Result> SalvarAsync(Inscricao inscricao, ComprovanteInscricao? comprovante, CancellationToken cancellationToken)
    {
        await inscricoes.AtualizarAsync(inscricao, cancellationToken).ConfigureAwait(false);
        if (comprovante is not null)
        {
            await inscricoes.AdicionarComprovanteAsync(comprovante, cancellationToken).ConfigureAwait(false);
        }

        return await ConfirmarAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result> ConfirmarAsync(CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(new DomainError(
                InscricaoErrorCodes.ConflitoDeConcorrencia, "Esta inscrição foi alterada concorrentemente. Tente novamente."));
        }
    }
}
