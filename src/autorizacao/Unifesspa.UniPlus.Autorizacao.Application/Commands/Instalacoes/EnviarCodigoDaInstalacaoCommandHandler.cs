namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do envio do código de confirmação: o envio, o resumo e a gravação são de <see cref="EnvioDoCodigoDeConfirmacao"/>,
/// e o e-mail sai como mensagem de saída, no outbox da mesma transação.
/// </summary>
public static class EnviarCodigoDaInstalacaoCommandHandler
{
    public static async Task<(Result<InstalacaoDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        EnviarCodigoDaInstalacaoCommand command,
        EnvioDoCodigoDeConfirmacao envio,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(envio);
        ArgumentNullException.ThrowIfNull(userContext);

        return IdentidadeDoToken.PessoaDe(userContext) is { } executor
            ? await envio.EnviarAsync(command.Id, executor, cancellationToken).ConfigureAwait(false)
            : (Result<InstalacaoDto>.Failure(InstalacaoHandlerErros.AutorNaoIdentificado()), []);
    }
}
