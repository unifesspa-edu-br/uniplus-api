namespace Unifesspa.UniPlus.Authorization.Decisao;

using System.Collections.Frozen;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;

/// <summary>
/// Ponto de decisão único (ADR-0078) na sua forma mínima: decide por concessão
/// efetiva, na ordem canônica do algoritmo, e registra toda decisão — permitida
/// ou negada — no registro operacional restrito.
/// </summary>
/// <remarks>
/// <para>
/// <b>Fatia mínima.</b> A única fonte de concessão em operação é o token
/// (<see cref="FonteGrant.Token"/>); vínculo de grupo e concessão excepcional
/// entram em incrementos próprios. Multifator e dupla aprovação também não estão
/// implementados — e por isso uma permissão que os exija é <b>recusada</b>, nunca
/// concedida: conceder o que não se sabe verificar é a falha aberta que o modelo
/// existe para evitar.
/// </para>
/// </remarks>
public sealed class AuthorizationDecisionService : IAuthorizationDecisionService
{
    private readonly GrantEfetivoAplicavelCheck _concessaoAplicavel;
    private readonly FrozenDictionary<string, IPermissaoCheck> _verificacoesDeContexto;
    private readonly IRegistroOperacionalRestrito _registro;

    /// <summary>
    /// Compõe o serviço com a verificação canônica de concessão (sempre
    /// executada) e as verificações contextuais registradas, indexadas pelo nome
    /// com que o catálogo as declara.
    /// </summary>
    public AuthorizationDecisionService(
        GrantEfetivoAplicavelCheck concessaoAplicavel,
        IEnumerable<IPermissaoCheck> verificacoesDeContexto,
        IRegistroOperacionalRestrito registro)
    {
        ArgumentNullException.ThrowIfNull(concessaoAplicavel);
        ArgumentNullException.ThrowIfNull(verificacoesDeContexto);
        ArgumentNullException.ThrowIfNull(registro);

        _concessaoAplicavel = concessaoAplicavel;
        _registro = registro;
        _verificacoesDeContexto = verificacoesDeContexto.ToFrozenDictionary(
            static check => check.Nome,
            StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public async Task<AuthorizationDecision> DecideAsync(
        AuthorizationSubject subject,
        PermissionRequirement requirement,
        ResourceContext resource,
        AuthorizationRequestContext request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(requirement);
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(request);

        AuthorizationDecision decisao = await AvaliarAsync(
            subject, requirement, resource, request, cancellationToken);

        // O registro é o último passo e nunca condiciona o veredito: quem
        // registra não decide. A porta não lança (ver IRegistroOperacionalRestrito),
        // de modo que uma falha de escrita não converte uma negativa em erro do
        // servidor nem derruba uma requisição legítima.
        _registro.Registrar(RegistroDecisaoAcesso.De(requirement, resource, request, decisao));

        return decisao;
    }

    private async Task<AuthorizationDecision> AvaliarAsync(
        AuthorizationSubject subject,
        PermissionRequirement requirement,
        ResourceContext resource,
        AuthorizationRequestContext request,
        CancellationToken cancellationToken)
    {
        // Pré-condição estrutural, antes da cadeia: sem os campos de contexto que
        // a permissão exige, as verificações seguintes decidiriam sobre um
        // recurso mal situado — e um escopo ausente pareceria escopo irrestrito.
        if (CamposDeContexto.AlgumAusente(requirement.EscopoContextoObrigatorio, resource))
        {
            return AuthorizationDecision.Negar(MotivoNegativa.ContextoObrigatorioAusente);
        }

        // Recusa incondicional enquanto não existe quem verifique o multifator.
        // AuthorizationSubject.MfaSatisfeito é preenchido por quem monta o
        // sujeito na borda; tomá-lo como prova faria um booleano informado pelo
        // chamador valer por uma verificação que ninguém executou. Quando houver
        // verificador, é ele que passa a decidir — não a afirmação de quem pede.
        if (requirement.RequerMfa)
        {
            return AuthorizationDecision.Negar(MotivoNegativa.MultifatorNaoSatisfeito);
        }

        // Mesma razão: a concessão de dupla aprovação que chega em
        // AuthorizationRequestContext ainda não tem quem lhe ateste validade e
        // não-reuso, e aceitá-la sob palavra vale menos que não a exigir.
        if (requirement.RequerDuplaAprovacao)
        {
            return AuthorizationDecision.Negar(MotivoNegativa.DuplaAprovacaoInvalida);
        }

        CheckResult concessao = await _concessaoAplicavel.CheckAsync(
            subject, requirement, resource, request, cancellationToken);

        if (!concessao.Passou)
        {
            return AuthorizationDecision.Negar(concessao.Motivo!.Value);
        }

        foreach (string nome in requirement.VerificacoesDeContexto)
        {
            CheckResult resultado = await LocalizarVerificacao(nome).CheckAsync(
                subject, requirement, resource, request, cancellationToken);

            if (!resultado.Passou)
            {
                return AuthorizationDecision.Negar(resultado.Motivo!.Value);
            }
        }

        // Dado pessoal só sai com base legal declarada (LGPD, art. 7º). A
        // sensibilidade é declarada em dois lugares — a permissão classifica o que
        // a operação retorna, o contexto classifica o recurso alcançado — e as
        // duas podem divergir. Vale a maior: olhar só uma delas deixaria uma
        // permissão classificada como sensível ser concedida sem base legal por
        // um contexto montado como interno.
        if ((ExigeBaseLegal(requirement.Sensibilidade) || ExigeBaseLegal(resource.Sensibilidade))
            && string.IsNullOrWhiteSpace(requirement.BaseLegalPadrao))
        {
            return AuthorizationDecision.Negar(MotivoNegativa.BaseLegalAusente);
        }

        return AuthorizationDecision.Permitir(concessao.GrantSelecionado!);
    }

    private static bool ExigeBaseLegal(Sensibilidade sensibilidade) =>
        sensibilidade is Sensibilidade.Pessoal or Sensibilidade.Sensivel;

    // Uma verificação declarada e não registrada é erro de composição, não
    // decisão de acesso: o catálogo recusa nome não registrado no build e o
    // registro do contêiner é conferido no boot, então chegar aqui significa que
    // uma dessas travas foi contornada. Traduzir isso em negativa esconderia o
    // defeito atrás de um 403 plausível — mesmo motivo pelo qual DenyReason.De
    // lança diante de um código fora do conjunto.
    private IPermissaoCheck LocalizarVerificacao(string nome) =>
        _verificacoesDeContexto.TryGetValue(nome, out IPermissaoCheck? check)
            ? check
            : throw new InvalidOperationException(
                $"A verificação de contexto '{nome}' é exigida pela permissão mas não está registrada.");
}
