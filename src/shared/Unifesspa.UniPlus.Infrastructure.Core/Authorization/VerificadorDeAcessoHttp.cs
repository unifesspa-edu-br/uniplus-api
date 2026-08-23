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

        HttpContext? http = _httpContextAccessor.HttpContext;

        // A correlação é a que o middleware propagou (X-Correlation-Id, quando a
        // requisição participa de um fluxo entre serviços), e não o identificador
        // local do ASP.NET: usá-lo daria ao registro da decisão um valor diferente
        // do que sai nos logs da aplicação e nas mensagens seguintes, justamente
        // impedindo a correlação que o campo existe para permitir.
        //
        // O instante do acesso é lido uma vez e acompanha a decisão inteira, de
        // modo que validade de concessão e registro falem do mesmo momento.
        Result<AuthorizationRequestContext> contexto = AuthorizationRequestContext.From(
            _correlationId.CorrelationId
                ?? http?.TraceIdentifier
                ?? Guid.CreateVersion7().ToString(),
            _relogio.GetUtcNow(),
            OrigemRequisicao.Api,
            ipOrigem: http?.Connection.RemoteIpAddress?.ToString(),
            userAgent: http?.Request.Headers.UserAgent.ToString());

        if (contexto.IsFailure)
        {
            return ResultadoDoAcesso.IdentidadeIncompleta;
        }

        Result<AuthorizationSubject> sujeito = await _resolver.ResolveAsync(
            _userContext, contexto.Value!, cancellationToken);

        // Sem emissor, subject ou jti não há sujeito sobre o qual decidir. A
        // falha é de autenticação, e devolvê-la como negativa faria a borda
        // responder 403 — afirmando uma decisão de acesso que não foi tomada.
        if (sujeito.IsFailure)
        {
            return ResultadoDoAcesso.IdentidadeIncompleta;
        }

        AuthorizationDecision decisao = await _decisao.DecideAsync(
            sujeito.Value!,
            permissao,
            recurso ?? RecursoDaPropriaOperacao(permissao),
            contexto.Value!,
            cancellationToken);

        return decisao.Allowed ? ResultadoDoAcesso.Permitido : ResultadoDoAcesso.Negado;
    }

    // Sem recurso informado, o alvo é a própria operação: o tipo é o código da
    // permissão e a sensibilidade é a que ela declara. Assim a decisão continua
    // recebendo um recurso real — e não um vazio que faria a verificação de
    // escopo passar por omissão.
    private static ResourceContext RecursoDaPropriaOperacao(PermissionRequirement permissao)
        => ResourceContext.From(permissao.Permissao, permissao.Sensibilidade).Value!;
}
