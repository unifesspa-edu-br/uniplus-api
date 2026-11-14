namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da confirmação do item: a concessão segue ativa e a decisão entra na trilha dela.</summary>
public static class ConfirmarItemDaRevisaoCommandHandler
{
    public static async Task<Result> Handle(
        ConfirmarItemDaRevisaoCommand command,
        PreparoDaDecisaoDoItem preparo,
        IVerificadorDeAcesso verificador,
        IUnitOfWork unitOfWork,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(preparo);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);

        Result<DecisaoDoItem> preparada = await preparo
            .PrepararAsync(command.RevisaoId, command.ItemId, verificador, IdentidadeDoToken.De(userContext), cancellationToken)
            .ConfigureAwait(false);

        return preparada.IsFailure
            ? Result.Failure(preparada.Error!)
            : await GravacaoDoItem.GravarAsync(Confirmar(preparada.Value!), unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static Result Confirmar(DecisaoDoItem d) => d.Revisao.Confirmar(d.Item.Id, d.Concessao, d.Decisor, d.Agora);
}
