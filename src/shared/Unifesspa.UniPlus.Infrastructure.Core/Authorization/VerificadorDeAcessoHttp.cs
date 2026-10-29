namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Infrastructure.Core.Middleware;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Liga o endpoint ao ponto de decisão: monta o sujeito, o recurso e o contexto
/// da requisição a partir do que já está em mãos e devolve o veredito.
/// </summary>
/// <remarks>
/// Sujeito que não se resolve — token sem emissor, sem <i>subject</i> ou sem
/// <c>jti</c> — não vira negativa de autorização: não houve decisão a tomar, e o
/// desfecho é <see cref="ResultadoDoAcesso.IdentidadeIncompleta"/>, que a borda
/// responde como <c>401</c>.
/// </remarks>
public sealed class VerificadorDeAcessoHttp : IVerificadorDeAcesso
{
    private readonly IUserContext _userContext;
    private readonly IAuthorizationSubjectResolver _resolver;
    private readonly IAuthorizationDecisionService _decisao;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICorrelationIdAccessor _correlationId;
    private readonly TimeProvider _relogio;

    /// <summary>Cria o verificador.</summary>
    public VerificadorDeAcessoHttp(
        IUserContext userContext,
        IAuthorizationSubjectResolver resolver,
        IAuthorizationDecisionService decisao,
        IHttpContextAccessor httpContextAccessor,
        ICorrelationIdAccessor correlationId,
        TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(decisao);
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        ArgumentNullException.ThrowIfNull(correlationId);
        ArgumentNullException.ThrowIfNull(relogio);

        _userContext = userContext;
        _resolver = resolver;
        _decisao = decisao;
        _httpContextAccessor = httpContextAccessor;
        _correlationId = correlationId;
        _relogio = relogio;
    }

    /// <inheritdoc />
    public async Task<ResultadoDoAcesso> VerificarAsync(
        PermissionRequirement permissao,
        ResourceContext? recurso = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permissao);

        (ResultadoDoAcesso? incompleta, AuthorizationSubject? sujeito, AuthorizationRequestContext? contexto) =
            await ResolverAsync(cancellationToken);

        if (incompleta is { } desfecho)
        {
            return desfecho;
        }

        return await DecidirAsync(
            sujeito!, permissao, recurso ?? RecursoDaPropriaOperacao(permissao), contexto!, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ResultadoDoAcesso> VerificarEmAlgumaUnidadeAsync(
        PermissionRequirement permissao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permissao);

        (ResultadoDoAcesso? incompleta, AuthorizationSubject? sujeito, AuthorizationRequestContext? contexto) =
            await ResolverAsync(cancellationToken);

        if (incompleta is { } desfecho)
        {
            return desfecho;
        }

        ResultadoDoAcesso resultado = ResultadoDoAcesso.Negado;

        foreach (Guid unidade in sujeito!.ConcessoesEfetivas
            .Where(concessao => string.Equals(concessao.PermissaoCodigo, permissao.Permissao, StringComparison.Ordinal))
            .Select(static concessao => concessao.EscopoUnidadeId)
            .OfType<Guid>()
            .Distinct())
        {
            ResourceContext recurso = ResourceContext.From(
                permissao.Permissao, permissao.Sensibilidade, unidadeProprietariaId: unidade).Value!;

            resultado = await DecidirAsync(sujeito, permissao, recurso, contexto!, cancellationToken);

            if (resultado == ResultadoDoAcesso.Permitido)
            {
                break;
            }
        }

        return resultado;
    }

    private async Task<(ResultadoDoAcesso? Incompleta, AuthorizationSubject? Sujeito, AuthorizationRequestContext? Contexto)> ResolverAsync(
        CancellationToken cancellationToken)
    {
        Result<AuthorizationRequestContext> contexto = ContextoDaRequisicaoHttp.Montar(
            _httpContextAccessor.HttpContext, _correlationId, _relogio);

        if (contexto.IsFailure)
        {
            return (ResultadoDoAcesso.IdentidadeIncompleta, null, null);
        }

        Result<AuthorizationSubject> sujeito = await _resolver.ResolveAsync(
            _userContext, contexto.Value!, cancellationToken);

        // Sem emissor, subject ou jti não há sujeito sobre o qual decidir. A
        // falha é de autenticação, e devolvê-la como negativa faria a borda
        // responder 403 — afirmando uma decisão de acesso que não foi tomada.
        return sujeito.IsFailure
            ? (ResultadoDoAcesso.IdentidadeIncompleta, null, null)
            : (null, sujeito.Value, contexto.Value);
    }

    private async Task<ResultadoDoAcesso> DecidirAsync(
        AuthorizationSubject sujeito,
        PermissionRequirement permissao,
        ResourceContext recurso,
        AuthorizationRequestContext contexto,
        CancellationToken cancellationToken)
    {
        AuthorizationDecision decisao = await _decisao.DecideAsync(
            sujeito, permissao, recurso, contexto, cancellationToken);

        if (decisao.Allowed)
        {
            return ResultadoDoAcesso.Permitido;
        }

        return decisao.DenyReason!.Codigo == MotivoNegativa.ImpedidoPorInscricaoNoProcesso
            ? ResultadoDoAcesso.Impedido
            : ResultadoDoAcesso.Negado;
    }

    // Sem recurso informado, o alvo é a própria operação: o tipo é o código da
    // permissão e a sensibilidade é a que ela declara. Assim a decisão continua
    // recebendo um recurso real — e não um vazio que faria a verificação de
    // escopo passar por omissão.
    private static ResourceContext RecursoDaPropriaOperacao(PermissionRequirement permissao)
        => ResourceContext.From(permissao.Permissao, permissao.Sensibilidade).Value!;
}
