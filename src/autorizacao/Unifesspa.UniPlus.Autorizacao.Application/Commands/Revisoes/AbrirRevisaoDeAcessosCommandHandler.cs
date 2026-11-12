namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da abertura. Só o Representante do controlador abre; a abertura em si é de <see cref="AberturaDaRevisao"/>.</summary>
public static class AbrirRevisaoDeAcessosCommandHandler
{
    public static async Task<Result<RevisaoDeAcessosDto>> Handle(
        AbrirRevisaoDeAcessosCommand command,
        AberturaDaRevisao abertura,
        IVerificadorDeAcesso verificador,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(abertura);
        ArgumentNullException.ThrowIfNull(userContext);

        AcessoDaRevisao? acesso = await AcessoDaRevisao.ConsultarAsync(verificador, null, cancellationToken).ConfigureAwait(false);
        if (acesso is null)
        {
            return Result<RevisaoDeAcessosDto>.Failure(RevisaoHandlerErros.IdentidadeIncompleta());
        }

        await AcessoDaRevisao.RegistrarAsync(verificador, [acesso.DecisaoConduzir], cancellationToken).ConfigureAwait(false);

        return await AbrirSePermitidoAsync(acesso, IdentidadeDoToken.De(userContext), abertura, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<RevisaoDeAcessosDto>> AbrirSePermitidoAsync(
        AcessoDaRevisao acesso, Identidade? abertaPor, AberturaDaRevisao abertura, CancellationToken cancellationToken)
    {
        if (abertaPor is null)
        {
            return Result<RevisaoDeAcessosDto>.Failure(RevisaoHandlerErros.AutorNaoIdentificado());
        }

        return acesso.Conduz
            ? await abertura.AbrirAsync(abertaPor, cancellationToken).ConfigureAwait(false)
            : Result<RevisaoDeAcessosDto>.Failure(RevisaoHandlerErros.SemPermissao());
    }
}
