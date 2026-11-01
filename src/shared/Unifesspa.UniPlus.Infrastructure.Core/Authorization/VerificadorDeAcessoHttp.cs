namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Infrastructure.Core.Middleware;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Liga o endpoint ao ponto de decisão: monta o sujeito, o resource e o contexto
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
    private readonly IRegistroOperacionalRestrito _registro;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICorrelationIdAccessor _correlationId;
    private readonly TimeProvider _relogio;

    /// <summary>Cria o verificador.</summary>
    public VerificadorDeAcessoHttp(
        IUserContext userContext,
        IAuthorizationSubjectResolver resolver,
        IAuthorizationDecisionService decisao,
        IRegistroOperacionalRestrito registro,
        IHttpContextAccessor httpContextAccessor,
        ICorrelationIdAccessor correlationId,
        TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(decisao);
        ArgumentNullException.ThrowIfNull(registro);
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        ArgumentNullException.ThrowIfNull(correlationId);
        ArgumentNullException.ThrowIfNull(relogio);

        _userContext = userContext;
        _resolver = resolver;
        _decisao = decisao;
        _registro = registro;
        _httpContextAccessor = httpContextAccessor;
        _correlationId = correlationId;
        _relogio = relogio;
    }

    /// <inheritdoc />
    public async Task<ResultadoDoAcesso> VerificarAsync(
        PermissionRequirement permissao,
        ResourceContext? resource = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permissao);

        (ResultadoDoAcesso? incompleta, AuthorizationSubject? sujeito, AuthorizationRequestContext? contexto) =
            await ResolverAsync(cancellationToken).ConfigureAwait(false);

        if (incompleta is { } desfecho)
        {
            return desfecho;
        }

        return await DecidirAsync(
            sujeito!, permissao, resource ?? ResourceDaPropriaOperacao(permissao), contexto!, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Decide por unidade sem registrar e grava num lote só o que vale: a decisão permitida que autorizou, e então
    /// as negativas anteriores são descartadas, porque a pergunta foi respondida; ou, se nenhuma unidade permitiu, as
    /// negativas de todas elas, que são a tentativa real. Assim a trilha não cresce com uma gravação por unidade.
    /// </remarks>
    public async Task<ResultadoDoAcesso> VerificarEmAlgumaUnidadeAsync(
        PermissionRequirement permissao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permissao);

        (ResultadoDoAcesso? incompleta, AuthorizationSubject? sujeito, AuthorizationRequestContext? contexto) =
            await ResolverAsync(cancellationToken).ConfigureAwait(false);

        if (incompleta is { } desfecho)
        {
            return desfecho;
        }

        ResultadoDoAcesso resultado = ResultadoDoAcesso.Negado;
        List<RegistroDecisaoAcesso> negadas = [];

        foreach (Guid unidade in sujeito!.ConcessoesEfetivas
            .Where(concessao => string.Equals(concessao.PermissaoCodigo, permissao.Permissao, StringComparison.Ordinal))
            .Select(static concessao => concessao.EscopoUnidadeId)
            .OfType<Guid>()
            .Distinct())
        {
            ResourceContext resource = ResourceContext.From(
                ResourceTipos.Operacao, permissao.Sensibilidade, unidadeProprietariaId: unidade).Value!;

            AuthorizationDecision decisao = await _decisao.DecidirSemRegistrarAsync(
                sujeito, permissao, resource, contexto!, cancellationToken).ConfigureAwait(false);
            RegistroDecisaoAcesso registro = RegistroDecisaoAcesso.De(sujeito, permissao, resource, contexto!, decisao);
            resultado = decisao.Resultado;

            if (decisao.Allowed)
            {
                await RegistrarSeExigidoAsync(permissao, resource, [registro], cancellationToken).ConfigureAwait(false);
                return resultado;
            }

            negadas.Add(registro);
        }

        // Nenhuma concessão da permissão em unidade: a tentativa existiu e grava-se uma decisão sobre a operação, sem
        // unidade. Quem não tem concessão alguma da permissão recebe a negativa de ausência de concessão; quem tem só
        // concessão global recebe o que o ponto de decisão devolver, sem afirmar o que não é verdade. O veredito
        // desta pergunta segue negado nos dois casos.
        if (negadas.Count == 0)
        {
            ResourceContext operacao = ResourceDaPropriaOperacao(permissao);
            bool temConcessao = sujeito.ConcessoesEfetivas.Any(
                concessao => string.Equals(concessao.PermissaoCodigo, permissao.Permissao, StringComparison.Ordinal));
            AuthorizationDecision decisaoSemUnidade = temConcessao
                ? await _decisao.DecidirSemRegistrarAsync(sujeito, permissao, operacao, contexto!, cancellationToken).ConfigureAwait(false)
                : AuthorizationDecision.Negar(MotivoNegativa.SemConcessaoAplicavel);

            // Nesta pergunta só conta a concessão em unidade: a decisão sem unidade que permitiria não é acesso
            // concedido, então o veredito segue negado e nada se afirma na trilha.
            if (!decisaoSemUnidade.Allowed)
            {
                negadas.Add(RegistroDecisaoAcesso.De(sujeito, permissao, operacao, contexto!, decisaoSemUnidade));
            }
        }

        if (negadas.Count > 0)
        {
            await RegistrarSeExigidoAsync(permissao, ResourceDaPropriaOperacao(permissao), negadas, cancellationToken)
                .ConfigureAwait(false);
        }

        return resultado;
    }

    /// <inheritdoc />
    public async Task<ResultadoDoAcesso> PodeAsync(
        PermissionRequirement permissao,
        ResourceContext? resource = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permissao);

        (ResultadoDoAcesso? incompleta, AuthorizationSubject? sujeito, AuthorizationRequestContext? contexto) =
            await ResolverAsync(cancellationToken).ConfigureAwait(false);

        if (incompleta is { } desfecho)
        {
            return desfecho;
        }

        return (await _decisao.DecidirSemRegistrarAsync(
            sujeito!, permissao, resource ?? ResourceDaPropriaOperacao(permissao), contexto!, cancellationToken).ConfigureAwait(false)).Resultado;
    }

    /// <inheritdoc />
    public async Task<DecisaoDeAcesso> DecidirSemRegistrarAsync(
        PermissionRequirement permissao,
        ResourceContext? resource = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permissao);

        (ResultadoDoAcesso? incompleta, AuthorizationSubject? sujeito, AuthorizationRequestContext? contexto) =
            await ResolverAsync(cancellationToken).ConfigureAwait(false);

        if (incompleta is not null)
        {
            return DecisaoDeAcesso.IdentidadeIncompleta;
        }

        ResourceContext alvo = resource ?? ResourceDaPropriaOperacao(permissao);
        AuthorizationDecision decisao = await _decisao.DecidirSemRegistrarAsync(
            sujeito!, permissao, alvo, contexto!, cancellationToken).ConfigureAwait(false);

        return DecisaoDeAcesso.Tomada(
            decisao.Resultado,
            RegistroDecisaoAcesso.De(sujeito!, permissao, alvo, contexto!, decisao));
    }

    /// <inheritdoc />
    public Task RegistrarAsync(IReadOnlyCollection<DecisaoDeAcesso> decisoes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decisoes);

        RegistroDecisaoAcesso[] registros = [.. decisoes.Select(static d => d.Registro).OfType<RegistroDecisaoAcesso>()];

        return registros.Length == 0 ? Task.CompletedTask : _registro.RegistrarAsync(registros, cancellationToken);
    }

    /// <inheritdoc />
    public Task RegistrarAsync(DecisaoDeAcesso permitida, CancellationToken cancellationToken = default)
        => _registro.RegistrarAsync([RegistroDaPermitida(permitida)], cancellationToken);

    /// <inheritdoc />
    public Task RegistrarParaCadaAsync(
        DecisaoDeAcesso permitida,
        string resourceTipo,
        IReadOnlyCollection<Guid> resourceIds,
        bool resourceEhInscricao = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resourceIds);

        return _registro.RegistrarAsync(
            RegistroDaPermitida(permitida).ParaCadaResource(resourceTipo, resourceIds, resourceEhInscricao),
            cancellationToken);
    }

    private Task RegistrarSeExigidoAsync(
        PermissionRequirement permissao,
        ResourceContext resource,
        IReadOnlyList<RegistroDecisaoAcesso> registros,
        CancellationToken cancellationToken) =>
        RegistroDecisaoAcesso.Exige(permissao, resource)
            ? _registro.RegistrarAsync(registros, cancellationToken)
            : Task.CompletedTask;

    private static RegistroDecisaoAcesso RegistroDaPermitida(DecisaoDeAcesso decisao)
    {
        ArgumentNullException.ThrowIfNull(decisao);

        return decisao is { Resultado: ResultadoDoAcesso.Permitido, Registro: { } registro }
            ? registro
            : throw new InvalidOperationException("Só a decisão permitida se registra por esta via.");
    }

    private async Task<(ResultadoDoAcesso? Incompleta, AuthorizationSubject? Sujeito, AuthorizationRequestContext? Contexto)> ResolverAsync(
        CancellationToken cancellationToken)
    {
        Result<AuthorizationRequestContext> contexto = ContextoDaRequisicaoHttp.Montar(
            _httpContextAccessor.HttpContext, _userContext, _correlationId, _relogio);

        if (contexto.IsFailure)
        {
            return (ResultadoDoAcesso.IdentidadeIncompleta, null, null);
        }

        Result<AuthorizationSubject> sujeito = await _resolver.ResolveAsync(
            _userContext, contexto.Value!, cancellationToken).ConfigureAwait(false);

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
        ResourceContext resource,
        AuthorizationRequestContext contexto,
        CancellationToken cancellationToken)
    {
        AuthorizationDecision decisao = await _decisao.DecideAsync(
            sujeito, permissao, resource, contexto, cancellationToken).ConfigureAwait(false);

        return decisao.Resultado;
    }

    // Sem resource informado, o alvo é a própria operação: o tipo é `operacao` e a
    // sensibilidade é a que a permissão declara. Assim a decisão continua
    // recebendo um resource real — e não um vazio que faria a verificação de
    // escopo passar por omissão.
    private static ResourceContext ResourceDaPropriaOperacao(PermissionRequirement permissao)
        => ResourceContext.From(ResourceTipos.Operacao, permissao.Sensibilidade).Value!;
}
