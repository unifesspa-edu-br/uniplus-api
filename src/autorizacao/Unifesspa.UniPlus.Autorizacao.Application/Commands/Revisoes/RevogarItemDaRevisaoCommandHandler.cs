namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler da revogação do item: revoga a concessão com efeito imediato e registra a decisão, na mesma gravação. Vale o
/// mínimo de dois gestores de acessos da revogação comum.
/// </summary>
public static class RevogarItemDaRevisaoCommandHandler
{
    public static async Task<Result> Handle(
        RevogarItemDaRevisaoCommand command,
        PreparoDaDecisaoDoItem preparo,
        IConcessaoRepository concessoes,
        IVerificadorDeAcesso verificador,
        IAutorizacaoUnitOfWork unitOfWork,
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
            : await RevogarAsync(preparada.Value!, command.Motivo, concessoes, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> RevogarAsync(
        DecisaoDoItem d, string? motivo, IConcessaoRepository concessoes, IAutorizacaoUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        Result revogacao = await RevogacaoDaConcessao
            .AplicarAsync(
                d.Concessao, d.Agora, () => d.Revisao.Revogar(d.Item.Id, d.Concessao, d.Decisor, motivo, d.Agora), concessoes, unitOfWork, cancellationToken)
            .ConfigureAwait(false);

        return await GravacaoDoItem.GravarAsync(revogacao, unitOfWork, cancellationToken).ConfigureAwait(false);
    }
}
