namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do passo 4: a conferência do código é de <see cref="TentativaDeConfirmacao"/>, e a concessão dos dois gestores,
/// de <see cref="ConcessaoDosGestores"/>. A recusa é <see cref="Result"/>, e a transação confirma a tentativa gravada.
/// </summary>
public static class ConfirmarInstalacaoCommandHandler
{
    public static async Task<Result> Handle(
        ConfirmarInstalacaoCommand command,
        TentativaDeConfirmacao tentativa,
        ConcessaoDosGestores gestores,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(tentativa);
        ArgumentNullException.ThrowIfNull(gestores);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (IdentidadeDoToken.De(userContext) is not { } autor)
        {
            return Result.Failure(InstalacaoHandlerErros.AutorNaoIdentificado());
        }

        DateTimeOffset agora = timeProvider.GetUtcNow();
        Result<Instalacao> aceita = await tentativa.ConferirAsync(autor, command.Codigo, agora, cancellationToken).ConfigureAwait(false);
        return aceita.IsFailure
            ? Result.ValidationFailure(aceita.Errors)
            : await gestores.ConcederAsync(aceita.Value!, autor, agora, cancellationToken).ConfigureAwait(false);
    }
}
