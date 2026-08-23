namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Authentication;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Monta o sujeito da decisão a partir do token OIDC autenticado (ADR-0078,
/// ADR-0033): identidade pelo par emissor + <i>subject</i>, sessão pelo
/// <c>jti</c>, e uma concessão efetiva de fonte <see cref="FonteGrant.Token"/>
/// para cada papel atribuído ao <i>client</i> da API.
/// </summary>
/// <remarks>
/// <para>
/// A fonte de permissão é <b>uma só</b>: <c>resource_access.{client}.roles</c>,
/// pelo <see cref="IUserContext.GetResourceRoles"/>. Os papéis de <i>realm</i>
/// (<see cref="IUserContext.Roles"/>) ficam reservados a responsabilidades
/// institucionais amplas e não viram permissão — misturar as duas listas faria um
/// papel administrativo geral conceder qualquer permissão de mesmo nome.
/// </para>
/// <para>
/// O <i>client</i> é o <c>clientId</c> deste resource server
/// (<c>Auth:ClientId</c>, <c>uniplus-api</c>), <b>não</b> a audiência
/// (<c>uniplus</c>). A ADR-0010 separa os dois de propósito: a audiência é a
/// claim compartilhada por todos os tokens da plataforma, enquanto o
/// <c>clientId</c> é a chave sob a qual o token aninha os papéis desta API. Ler
/// os papéis sob a audiência não encontra nada — e negar tudo em silêncio é
/// exatamente o modo de falhar que a ADR-0010 antecipou ao listar a confusão
/// entre os dois como risco.
/// </para>
/// <para>
/// A opção é lida por <see cref="IOptions{TOptions}"/>, a mesma forma fixa com
/// que a validação do token é configurada, e não por
/// <c>IOptionsMonitor</c>. Sob recarga de configuração, a leitura monitorada
/// mudaria aqui sem mudar lá: a autenticação seguiria aceitando tokens da
/// audiência antiga enquanto a decisão passaria a colher papéis da nova — e um
/// token emitido para um <i>client</i> que também carregue papéis de outro
/// obteria permissões que não lhe cabem.
/// </para>
/// </remarks>
public sealed class HttpAuthorizationSubjectResolver : IAuthorizationSubjectResolver
{
    private readonly IOptions<AuthOptions> _authOptions;

    /// <summary>Cria o agregador do sujeito.</summary>
    public HttpAuthorizationSubjectResolver(IOptions<AuthOptions> authOptions)
    {
        ArgumentNullException.ThrowIfNull(authOptions);

        _authOptions = authOptions;
    }

    /// <inheritdoc />
    public Task<Result<AuthorizationSubject>> ResolveAsync(
        IUserContext userContext,
        AuthorizationRequestContext request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(Resolver(userContext));
    }

    private Result<AuthorizationSubject> Resolver(IUserContext userContext)
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

        List<EffectiveGrant> concessoes = [];
        foreach (string papel in userContext.GetResourceRoles(_authOptions.Value.ClientId))
        {
            Result<EffectiveGrant> concessao = EffectiveGrant.From(papel, FonteGrant.Token);

            // Um papel em branco no token não invalida os demais: a concessão que
            // ele descreveria não existe, e as outras continuam legítimas.
            if (concessao.IsSuccess)
            {
                concessoes.Add(concessao.Value!);
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
