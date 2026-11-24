namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDocumento;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarTipoDocumentoCommand"/> (convention-based Wolverine): confere a declaração inteira
/// primeiro (sem I/O), só então a existência da categoria e a unicidade do código, e grava
/// (<see cref="GravacaoDoTipoDocumento"/>).
/// </summary>
public static class CriarTipoDocumentoCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarTipoDocumentoCommand command,
        GravacaoDoTipoDocumento gravacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(gravacao);

        Result<TipoDocumento> tipo = TipoDocumento.Criar(command.Declaracao());

        return tipo.IsFailure
            ? Result<Guid>.ValidationFailure(tipo.Errors)
            : await gravacao.IncluirAsync(tipo.Value!, cancellationToken).ConfigureAwait(false);
    }
}
