namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDocumento;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="AtualizarTipoDocumentoCommand"/>: a conferência dos campos vem antes de buscar o tipo, e a
/// categoria e o código só são conferidos contra o cadastro quando mudam (<see cref="GravacaoDoTipoDocumento"/>).
/// </summary>
public static class AtualizarTipoDocumentoCommandHandler
{
    public static Task<Result> Handle(
        AtualizarTipoDocumentoCommand command,
        GravacaoDoTipoDocumento gravacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(gravacao);

        return gravacao.EditarAsync(command.Id, command.Declaracao(), cancellationToken);
    }
}
