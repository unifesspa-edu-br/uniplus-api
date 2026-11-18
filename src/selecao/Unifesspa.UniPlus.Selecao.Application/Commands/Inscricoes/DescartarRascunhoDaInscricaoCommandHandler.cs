namespace Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;

using Domain.Entities;

using Kernel.Results;

using Services;
using Services.Inscricoes;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>Remove o rascunho do titular e confirma a remoção junto da agenda de limpeza dos arquivos.</summary>
public static class DescartarRascunhoDaInscricaoCommandHandler
{
    public static async Task<Result> Handle(
        DescartarRascunhoDaInscricaoCommand command, InscricaoDoDono dono,
        DescarteDaInscricao descarte, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(dono);
        ArgumentNullException.ThrowIfNull(descarte);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        Result<Inscricao> inscricao = await dono.ObterAtivaAsync(command.ProcessoSeletivoId, "descartar a inscrição", cancellationToken).ConfigureAwait(false);
        return inscricao.IsFailure
            ? Result.Failure(inscricao.Error!)
            : await DescartarEConfirmarAsync(inscricao.Value!, command, descarte, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> DescartarEConfirmarAsync(
        Inscricao inscricao, DescartarRascunhoDaInscricaoCommand command,
        DescarteDaInscricao descarte, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        Result resultado = await descarte.DescartarAsync(inscricao, command.Precondicao, cancellationToken).ConfigureAwait(false);
        if (resultado.IsSuccess)
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        return resultado;
    }
}
