namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Os handlers da suspensão e da retomada; os casos de uso vivem em <see cref="ContencaoPorSuspensao"/>.</summary>
public static class ContencaoPorSuspensaoCommandHandler
{
    public static async Task<(Result Resposta, IEnumerable<object> Mensagens)> Handle(
        SuspenderPerfilCommand command,
        ContencaoPorSuspensao contencao,
        IVerificadorDeAcesso verificador,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(contencao);

        GravacaoDaOperacao<bool> gravacao = await contencao
            .SuspenderAsync(command.PerfilId, command.Motivo, verificador, cancellationToken)
            .ConfigureAwait(false);

        return (gravacao.Resultado.IsSuccess ? Result.Success() : Result.ValidationFailure(gravacao.Resultado.Errors), gravacao.Avisos);
    }

    public static Task<Result<RetomadaDaSuspensaoDto>> Handle(
        RetomarSuspensaoDoPerfilCommand command,
        ContencaoPorSuspensao contencao,
        IVerificadorDeAcesso verificador,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(contencao);

        return contencao.RetomarAsync(command.PerfilId, command.Etapa, command.Motivo, verificador, cancellationToken);
    }
}
