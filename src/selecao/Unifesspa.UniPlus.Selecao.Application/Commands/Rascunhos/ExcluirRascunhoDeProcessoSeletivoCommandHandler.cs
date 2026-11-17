namespace Unifesspa.UniPlus.Selecao.Application.Commands.Rascunhos;

using Kernel.Results;

using Services.Rascunhos;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Handler do <see cref="ExcluirRascunhoDeProcessoSeletivoCommand"/>: precondição ausente, bloqueio da linha, processo já publicado
/// ou inexistente, precondição que não casa, e só então apaga a linha. Sem chave de idempotência, a exceção de concorrência propaga.
/// </summary>
public static class ExcluirRascunhoDeProcessoSeletivoCommandHandler
{
    public static async Task<Result> Handle(
        ExcluirRascunhoDeProcessoSeletivoCommand command,
        BloqueioDoRascunhoDeProcessoSeletivo bloqueio,
        IRascunhoDeProcessoSeletivoRepository rascunhos,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(bloqueio);
        ArgumentNullException.ThrowIfNull(rascunhos);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<RascunhoDeProcessoSeletivo> rascunho = await bloqueio.BloquearEConferirAsync(command.Id, command.Precondicao, cancellationToken).ConfigureAwait(false);
        if (rascunho.IsFailure)
        {
            return Result.ValidationFailure(rascunho.Errors);
        }

        rascunhos.Remover(rascunho.Value!);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
