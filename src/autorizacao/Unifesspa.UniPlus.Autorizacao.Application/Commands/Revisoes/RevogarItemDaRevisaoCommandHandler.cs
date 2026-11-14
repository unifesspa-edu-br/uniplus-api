namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler da revogação do item: revoga a concessão com efeito imediato e registra a decisão, na mesma gravação. Valem as
/// regras da revogação comum: o mínimo de dois gestores de acessos e a do último Representante do controlador
/// (UNI-REQ-0394), conferida sob a trava do papel que o preparo da decisão já tomou.
/// </summary>
public static class RevogarItemDaRevisaoCommandHandler
{
    public static async Task<Result> Handle(
        RevogarItemDaRevisaoCommand command,
        PreparoDaDecisaoDoItem preparo,
        RevogacaoDaConcessao revogacao,
        IVerificadorDeAcesso verificador,
        IUnitOfWork unitOfWork,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(preparo);
        ArgumentNullException.ThrowIfNull(revogacao);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);

        Result<DecisaoDoItem> preparada = await preparo
            .PrepararAsync(command.RevisaoId, command.ItemId, verificador, IdentidadeDoToken.De(userContext), cancellationToken)
            .ConfigureAwait(false);

        return preparada.IsFailure
            ? Result.Failure(preparada.Error!)
            : await RevogarAsync(preparada.Value!, command.Motivo, revogacao, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> RevogarAsync(
        DecisaoDoItem d, string? motivo, RevogacaoDaConcessao revogacao, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        Result revogada = await revogacao
            .AplicarAsync(d.Concessao, d.Agora, () => d.Revisao.Revogar(d.Item.Id, d.Concessao, d.Decisor, motivo, d.Agora), cancellationToken)
            .ConfigureAwait(false);

        return await GravacaoDoItem.GravarAsync(revogada, unitOfWork, cancellationToken).ConfigureAwait(false);
    }
}
