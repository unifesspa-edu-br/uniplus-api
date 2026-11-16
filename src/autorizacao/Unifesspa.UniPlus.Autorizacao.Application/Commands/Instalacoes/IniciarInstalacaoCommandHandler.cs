namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do passo 1: a conferência é de <see cref="ConferenciaDaNovaInstalacao"/>, e a gravação, de
/// <see cref="GravacaoDaNovaInstalacao"/>. A recusa por já haver gestor sai antes de tudo: com gestor ativo nenhuma
/// instalação vale, e corrigir o resto não leva a nada.
/// </summary>
public static class IniciarInstalacaoCommandHandler
{
    public static async Task<Result<InstalacaoIniciadaDto>> Handle(
        IniciarInstalacaoCommand command,
        ConferenciaDaNovaInstalacao conferencia,
        GravacaoDaNovaInstalacao gravacao,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(conferencia);
        ArgumentNullException.ThrowIfNull(gravacao);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (IdentidadeDoToken.De(userContext) is not { } executor)
        {
            return Result<InstalacaoIniciadaDto>.Failure(InstalacaoHandlerErros.AutorNaoIdentificado());
        }

        DateTimeOffset agora = timeProvider.GetUtcNow();
        Result<Instalacao> conferida = await conferencia.ConferirAsync(command, executor, agora, cancellationToken).ConfigureAwait(false);
        return conferida.IsFailure
            ? Result<InstalacaoIniciadaDto>.ValidationFailure(conferida.Errors)
            : await gravacao.GravarAsync(conferida.Value!, executor, agora, cancellationToken).ConfigureAwait(false);
    }
}
