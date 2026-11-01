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
    public async Task<ResultadoDoAcesso> ExigirAsync(
        PermissionRequirement requisito,
        ResourceContext? resource = null,
        CancellationToken cancellationToken = default)
    {
        DecisaoDeAcesso decisao = await AvaliarAsync(requisito, resource, cancellationToken).ConfigureAwait(false);
        await RegistrarDesfechoAsync([decisao], IntencaoDoRegistro.Pedido, cancellationToken).ConfigureAwait(false);

        return decisao.Resultado;
    }

    /// <inheritdoc />
    public async Task<ResultadoDoAcesso> PodeAsync(
        PermissionRequirement requisito,
        ResourceContext? resource = null,
        CancellationToken cancellationToken = default) =>
        (await AvaliarAsync(requisito, resource, cancellationToken).ConfigureAwait(false)).Resultado;

    /// <inheritdoc />
    public async Task<DecisaoDeAcesso> AvaliarAsync(
        PermissionRequirement requisito,
        ResourceContext? resource = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisito);

        if (await ResolverAsync(cancellationToken).ConfigureAwait(false) is not var (sujeito, contexto))
        {
            return SemSujeito.Instancia;
        }

        return await DecidirAsync(
            sujeito, contexto, requisito, resource ?? ResourceDaPropriaOperacao(requisito), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DecisaoDeAcesso>> AvaliarEmAlgumaUnidadeAsync(
        PermissionRequirement requisito,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisito);

        if (await ResolverAsync(cancellationToken).ConfigureAwait(false) is not var (sujeito, contexto))
        {
            return [SemSujeito.Instancia];
        }

        List<DecisaoDeAcesso> decisoes = [];
        foreach (Guid unidade in sujeito.ConcessoesEfetivas
            .Where(concessao => string.Equals(concessao.PermissaoCodigo, requisito.Permissao, StringComparison.Ordinal))
            .Select(static concessao => concessao.EscopoUnidadeId)
            .OfType<Guid>()
            .Distinct())
        {
            ResourceContext resource = ResourceContext.From(
                ResourceTipos.Operacao, requisito.Sensibilidade, unidadeProprietariaId: unidade).Value!;

            DecisaoDeAcesso decisao = await DecidirAsync(sujeito, contexto, requisito, resource, cancellationToken).ConfigureAwait(false);
            decisoes.Add(decisao);

            if (decisao is DecisaoPermitida)
            {
                return decisoes;
            }
        }

        if (decisoes.Count > 0)
        {
            return decisoes;
        }

        // Sem concessão da permissão em unidade, a decisão é a da operação sem unidade. Quem não tem concessão alguma
        // da permissão recebe a negativa de ausência de concessão. A concessão global, ainda que permitisse, não
        // autoriza a alternativa, que exige concessão em unidade: a decisão que permitiria é negada com o motivo
        // próprio, em vez de afirmar uma falta de concessão que não existe.
        ResourceContext operacao = ResourceDaPropriaOperacao(requisito);
        if (!sujeito.ConcessoesEfetivas.Any(
            concessao => string.Equals(concessao.PermissaoCodigo, requisito.Permissao, StringComparison.Ordinal)))
        {
            return [Negar(sujeito, contexto, requisito, operacao, MotivoNegativa.SemConcessaoAplicavel)];
        }

        DecisaoDeAcesso semUnidade = await DecidirAsync(sujeito, contexto, requisito, operacao, cancellationToken).ConfigureAwait(false);

        return [semUnidade is DecisaoPermitida
            ? Negar(sujeito, contexto, requisito, operacao, MotivoNegativa.ConcessaoEmUnidadeExigida)
            : semUnidade];
    }

    /// <inheritdoc />
    public Task RegistrarDesfechoAsync(
        IReadOnlyCollection<DecisaoDeAcesso> decisoes,
        IntencaoDoRegistro intencao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decisoes);

        return GravarAsync(PoliticaDeRegistroDeAcesso.Desfecho(decisoes, intencao), cancellationToken);
    }

    /// <inheritdoc />
    public Task RegistrarParaCadaAsync(
        DecisaoPermitida permitida,
        string resourceTipo,
        IReadOnlyCollection<Guid> resourceIds,
        bool resourceEhInscricao = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permitida);
        ArgumentNullException.ThrowIfNull(resourceIds);

        return GravarAsync(
            PoliticaDeRegistroDeAcesso.PorResource(permitida, resourceTipo, resourceIds, resourceEhInscricao), cancellationToken);
    }

    private Task GravarAsync(IReadOnlyList<RegistroDecisaoAcesso> registros, CancellationToken cancellationToken) =>
        registros.Count == 0 ? Task.CompletedTask : _registro.RegistrarAsync(registros, cancellationToken);

    // Sem emissor, subject ou jti não há sujeito sobre o qual decidir. A falha é de autenticação, e devolvê-la como
    // negativa faria a borda responder 403 — afirmando uma decisão de acesso que não foi tomada.
    private async Task<(AuthorizationSubject Sujeito, AuthorizationRequestContext Contexto)?> ResolverAsync(
        CancellationToken cancellationToken)
    {
        Result<AuthorizationRequestContext> contexto = ContextoDaRequisicaoHttp.Montar(
            _httpContextAccessor.HttpContext, _userContext, _correlationId, _relogio);

        if (contexto.IsFailure)
        {
            return null;
        }

        Result<AuthorizationSubject> sujeito = await _resolver.ResolveAsync(
            _userContext, contexto.Value!, cancellationToken).ConfigureAwait(false);

        return sujeito.IsFailure ? null : (sujeito.Value!, contexto.Value!);
    }

    private async Task<DecisaoDeAcesso> DecidirAsync(
        AuthorizationSubject sujeito,
        AuthorizationRequestContext contexto,
        PermissionRequirement requisito,
        ResourceContext resource,
        CancellationToken cancellationToken)
    {
        AuthorizationDecision decisao = await _decisao.DecidirAsync(
            sujeito, requisito, resource, contexto, cancellationToken).ConfigureAwait(false);

        return DecisaoDeAcesso.Tomada(
            requisito, resource, RegistroDecisaoAcesso.De(sujeito, requisito, resource, contexto, decisao));
    }

    private static DecisaoDeAcesso Negar(
        AuthorizationSubject sujeito,
        AuthorizationRequestContext contexto,
        PermissionRequirement requisito,
        ResourceContext resource,
        MotivoNegativa motivo) =>
        DecisaoDeAcesso.Tomada(
            requisito,
            resource,
            RegistroDecisaoAcesso.De(sujeito, requisito, resource, contexto, AuthorizationDecision.Negar(motivo)));

    // Sem resource informado, o alvo é a própria operação: o tipo é `operacao` e a
    // sensibilidade é a que a permissão declara. Assim a decisão continua
    // recebendo um resource real — e não um vazio que faria a verificação de
    // escopo passar por omissão.
    private static ResourceContext ResourceDaPropriaOperacao(PermissionRequirement permissao)
        => ResourceContext.From(ResourceTipos.Operacao, permissao.Sensibilidade).Value!;
}
