namespace Unifesspa.UniPlus.Autorizacao.Application.Commands;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O processamento de um perfil por uma rotina: cada perfil vai num comando próprio, a falha de um é isolada e só o
/// perfil que teve o que fazer conta como tratado. O cancelamento não é falha e se propaga.
/// </summary>
internal static class ProcessamentoDoPerfil
{
    public static async Task<int> ExecutarAsync(
        ICommandBus bus, ICommand<Result<bool>> comando, Action<Exception?> falhou, CancellationToken cancellationToken)
    {
        try
        {
            return Contagem(await bus.Send(comando, cancellationToken).ConfigureAwait(false), falhou);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            falhou(ex);

            return 0;
        }
    }

    private static int Contagem(Result<bool> resultado, Action<Exception?> falhou)
    {
        if (resultado.IsFailure)
        {
            falhou(null);
        }

        return resultado.IsSuccess && resultado.Value ? 1 : 0;
    }
}
