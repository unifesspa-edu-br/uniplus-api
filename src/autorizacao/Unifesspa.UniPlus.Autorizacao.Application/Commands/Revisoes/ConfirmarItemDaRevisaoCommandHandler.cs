namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da confirmação do item: a concessão segue ativa e a decisão entra na trilha dela.</summary>
public static class ConfirmarItemDaRevisaoCommandHandler
{
    public static async Task<Result> Handle(
        ConfirmarItemDaRevisaoCommand command,
        IRevisaoDeAcessosRepository revisoes,
        IConcessaoRepository concessoes,
        IVerificadorDeAcesso verificador,
        IAutorizacaoUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(revisoes);
        ArgumentNullException.ThrowIfNull(concessoes);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Result<DecisaoDoItem> preparada = await DecisaoDoItem
            .PrepararAsync(command.RevisaoId, command.ItemId, revisoes, concessoes, verificador, IdentidadeDoToken.De(userContext), cancellationToken)
            .ConfigureAwait(false);
        if (preparada.IsFailure)
        {
            return Result.Failure(preparada.Error!);
        }

        (RevisaoDeAcessos revisao, ItemDaRevisao item, Concessao concessao, Identidade decisor) = preparada.Value!;

        Result confirmada = revisao.Confirmar(item.Id, concessao, decisor, timeProvider.GetUtcNow());
        if (confirmada.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return confirmada;
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ViolacoesDeGravacao.EhConflitoDeConcorrencia(ex))
        {
            // Com Idempotency-Key o handler captura e descarta o rastreamento (ADR-0119).
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(RevisaoHandlerErros.ConflitoDeConcorrencia());
        }

        return Result.Success();
    }
}
