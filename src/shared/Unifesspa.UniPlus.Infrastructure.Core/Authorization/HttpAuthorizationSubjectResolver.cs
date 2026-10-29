namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Monta o sujeito da decisão (ADR-0078, ADR-0033): identidade pelo par emissor
/// + <i>subject</i> do token OIDC autenticado, sessão pelo <c>jti</c>, e uma
/// concessão efetiva de fonte <see cref="FonteGrant.Concessao"/> para cada
/// permissão de cada concessão gravada e não revogada da pessoa.
/// </summary>
/// <remarks>
/// O token identifica a pessoa e não concede acesso (ADR-0152): papel de
/// <i>client</i> ou de <i>realm</i>, inclusive <c>plataforma-admin</c>, não vira
/// concessão. Quem recebe um papel direto no provedor de identidade não ganha
/// permissão alguma do catálogo, e a revogação da concessão gravada vale na
/// requisição seguinte, sem esperar o token expirar.
/// </remarks>
public sealed class HttpAuthorizationSubjectResolver : IAuthorizationSubjectResolver
{
    private readonly IConcessoesAtivasDoSujeito _concessoes;

    /// <summary>Cria o agregador do sujeito.</summary>
    public HttpAuthorizationSubjectResolver(IConcessoesAtivasDoSujeito concessoes)
    {
        ArgumentNullException.ThrowIfNull(concessoes);

        _concessoes = concessoes;
    }

    /// <inheritdoc />
    public async Task<Result<AuthorizationSubject>> ResolveAsync(
        IUserContext userContext,
        AuthorizationRequestContext request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(request);

        return await ResolverAsync(userContext, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<AuthorizationSubject>> ResolverAsync(IUserContext userContext, CancellationToken cancellationToken)
    {
        if (!userContext.IsAuthenticated)
        {
            return Falha(
                AuthorizationErrorCodes.SujeitoNaoAutenticado,
                "Requisição sem principal autenticado — não há sujeito para decidir.");
        }

        if (string.IsNullOrWhiteSpace(userContext.Issuer))
        {
            return Falha(
                AuthorizationErrorCodes.SujeitoIssuerAusente,
                "Token autenticado sem o emissor (iss) que identifica o sujeito.");
        }

        if (string.IsNullOrWhiteSpace(userContext.UserId))
        {
            return Falha(
                AuthorizationErrorCodes.SujeitoSubjectAusente,
                "Token autenticado sem o subject (sub) que identifica o sujeito.");
        }

        // Sem jti não há como referir a sessão numa revogação nem na trilha; o
        // contrato do sujeito o exige, e recusar aqui produz uma falha explicada
        // em vez de deixar a fábrica reprovar com uma mensagem genérica.
        if (string.IsNullOrWhiteSpace(userContext.Jti))
        {
            return Falha(
                AuthorizationErrorCodes.SujeitoJtiAusente,
                "Token autenticado sem o identificador do token (jti).");
        }

        Result<UsuarioRef> usuario = UsuarioRef.From(userContext.Issuer, userContext.UserId);
        if (usuario.IsFailure)
        {
            return Result<AuthorizationSubject>.Failure(usuario.Error!);
        }

        IReadOnlyList<ConcessaoAtiva> gravadas = await _concessoes
            .ListarAsync(usuario.Value!, cancellationToken)
            .ConfigureAwait(false);

        List<EffectiveGrant> concessoes = [];
        foreach (ConcessaoAtiva gravada in gravadas)
        {
            foreach (string permissao in gravada.Permissoes)
            {
                Result<EffectiveGrant> concessao = EffectiveGrant.From(
                    permissao,
                    FonteGrant.Concessao,
                    grantId: gravada.ConcessaoId,
                    escopoUnidadeId: gravada.UnidadeId,
                    validoAte: gravada.ValidaAte,
                    escopoInstitucional: gravada.Institucional,
                    perfilId: gravada.PerfilId);

                // Uma permissão em branco no perfil não invalida as demais: a concessão que ela
                // descreveria não existe, e as outras continuam legítimas.
                if (concessao.IsSuccess)
                {
                    concessoes.Add(concessao.Value!);
                }
            }
        }

        return AuthorizationSubject.From(
            usuario.Value!,
            userContext.Jti,
            concessoesEfetivas: concessoes);
    }

    private static Result<AuthorizationSubject> Falha(string codigo, string mensagem)
        => Result<AuthorizationSubject>.Failure(new DomainError(codigo, mensagem));
}
