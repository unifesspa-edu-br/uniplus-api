namespace Unifesspa.UniPlus.Authorization.ValueObjects;

using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Referência ao usuário (sujeito) de uma decisão de autorização (ADR-0078,
/// ADR-0033). O sujeito é identificado pelo par <see cref="Emissor"/> +
/// <see cref="Subject"/> do token OIDC — o <see cref="Subject"/> é uma string
/// <b>opaca</b> e <b>nunca</b> um <see cref="Guid"/>: identificadores de
/// provedores externos (Gov.br, Keycloak) não têm forma de GUID, e tipar como
/// GUID rejeitaria silenciosamente sujeitos válidos. O <see cref="UsuarioId"/>
/// é o identificador interno opcional, quando o usuário já está conhecido.
/// </summary>
public sealed record UsuarioRef
{
    /// <summary>Emissor do token (issuer OIDC).</summary>
    public string Emissor { get; }

    /// <summary>Sujeito (claim <c>sub</c>) — string opaca, nunca um GUID.</summary>
    public string Subject { get; }

    /// <summary>Identificador interno do usuário, quando já resolvido. Opcional.</summary>
    public Guid? UsuarioId { get; }

    private UsuarioRef(string emissor, string subject, Guid? usuarioId)
    {
        Emissor = emissor;
        Subject = subject;
        UsuarioId = usuarioId;
    }

    /// <summary>
    /// Constrói uma <see cref="UsuarioRef"/> validada. Rejeita emissor ou
    /// sujeito vazios.
    /// </summary>
    public static Result<UsuarioRef> From(string? emissor, string? subject, Guid? usuarioId = null)
    {
        if (string.IsNullOrWhiteSpace(emissor))
        {
            return Result<UsuarioRef>.Failure(new DomainError(
                AuthorizationErrorCodes.UsuarioRefEmissorObrigatorio,
                "Emissor do usuário é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            return Result<UsuarioRef>.Failure(new DomainError(
                AuthorizationErrorCodes.UsuarioRefSubjectObrigatorio,
                "Subject do usuário é obrigatório."));
        }

        return Result<UsuarioRef>.Success(new UsuarioRef(emissor.Trim(), subject.Trim(), usuarioId));
    }
}
