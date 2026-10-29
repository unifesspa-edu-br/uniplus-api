namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Middleware;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Resolve o sujeito do token pelo mesmo agregador que a decisão usa e devolve as
/// permissões do catálogo que as suas concessões vigentes cobrem.
/// </summary>
public sealed class PermissoesEfetivasHttp : IPermissoesEfetivas
{
    private readonly IUserContext _userContext;
    private readonly IAuthorizationSubjectResolver _resolver;
    private readonly CatalogoDaAplicacao _catalogo;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICorrelationIdAccessor _correlationId;
    private readonly TimeProvider _relogio;

    /// <summary>Cria o serviço.</summary>
    public PermissoesEfetivasHttp(
        IUserContext userContext,
        IAuthorizationSubjectResolver resolver,
        CatalogoDaAplicacao catalogo,
        IHttpContextAccessor httpContextAccessor,
        ICorrelationIdAccessor correlationId,
        TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        ArgumentNullException.ThrowIfNull(correlationId);
        ArgumentNullException.ThrowIfNull(relogio);

        _userContext = userContext;
        _resolver = resolver;
        _catalogo = catalogo;
        _httpContextAccessor = httpContextAccessor;
        _correlationId = correlationId;
        _relogio = relogio;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DefinicaoDePermissao>?> ObterAsync(CancellationToken cancellationToken = default)
    {
        Result<AuthorizationRequestContext> contexto = ContextoDaRequisicaoHttp.Montar(
            _httpContextAccessor.HttpContext, _correlationId, _relogio);

        if (contexto.IsFailure)
        {
            return null;
        }

        Result<AuthorizationSubject> sujeito = await _resolver
            .ResolveAsync(_userContext, contexto.Value!, cancellationToken)
            .ConfigureAwait(false);

        if (sujeito.IsFailure)
        {
            return null;
        }

        DateTimeOffset agora = contexto.Value!.DataAcesso;
        HashSet<string> vigentes = [.. sujeito.Value!.ConcessoesEfetivas
            .Where(concessao => concessao.ValidoAte is not { } validade || validade > agora)
            .Select(static concessao => concessao.PermissaoCodigo)];

        return [.. _catalogo.Definicoes.Where(d => vigentes.Contains(d.Codigo))];
    }
}
