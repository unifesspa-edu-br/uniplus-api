namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Termos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da publicação; a regra é de <see cref="PublicacaoDoTermo"/> e do agregado do termo.</summary>
public static class PublicarVersaoDoTermoCommandHandler
{
    public static async Task<Result<VersaoDoTermoDto>> Handle(
        PublicarVersaoDoTermoCommand command,
        PublicacaoDoTermo publicacao,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(publicacao);
        ArgumentNullException.ThrowIfNull(userContext);

        return IdentidadeDoToken.De(userContext) is { } autor
            ? await publicacao.PublicarAsync(command, autor, cancellationToken).ConfigureAwait(false)
            : Result<VersaoDoTermoDto>.Failure(TermoHandlerErros.AutorNaoIdentificado());
    }
}
