namespace Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;

using Domain.Entities;
using Domain.Errors;

using DTOs.Comprovantes;

using Kernel.Results;

using Services;
using Services.Inscricoes;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>Envia ou corrige a inscrição do titular e confirma todas as evidências na mesma transação.</summary>
public static class SubmeterInscricaoCommandHandler
{
    public static async Task<Result<ReciboDaInscricaoDto>> Handle(
        SubmeterInscricaoCommand command,
        InscricaoDoDono inscricaoDoDono,
        SubmissaoDaInscricao submissao,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(inscricaoDoDono);
        ArgumentNullException.ThrowIfNull(submissao);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        Result<Inscricao> inscricao = await inscricaoDoDono.ObterAtivaAsync(
            command.ProcessoSeletivoId, "enviar a inscrição", cancellationToken).ConfigureAwait(false);
        return inscricao.IsFailure
            ? Result<ReciboDaInscricaoDto>.Failure(inscricao.Error!)
            : await SubmeterEConfirmarAsync(inscricao.Value!, command, submissao, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<ReciboDaInscricaoDto>> SubmeterEConfirmarAsync(
        Inscricao inscricao, SubmeterInscricaoCommand command, SubmissaoDaInscricao submissao,
        IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        Result<ReciboDaInscricaoDto> resultado = await submissao.SubmeterAsync(inscricao, command, cancellationToken).ConfigureAwait(false);
        if (resultado.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return resultado;
        }
        return await ConfirmarAsync(resultado, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<ReciboDaInscricaoDto>> ConfirmarAsync(
        Result<ReciboDaInscricaoDto> resultado, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return resultado;
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<ReciboDaInscricaoDto>.Failure(new DomainError(
                InscricaoErrorCodes.ConflitoDeConcorrencia, "Esta inscrição foi alterada concorrentemente. Tente novamente."));
        }
    }
}
