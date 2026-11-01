namespace Unifesspa.UniPlus.Authorization.Contracts;

using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;

/// <summary>
/// Resultado do ponto de decisão único de autorização (ADR-0078). Carrega o
/// veredito (<see cref="Allowed"/>), o motivo estruturado quando nega
/// (<see cref="DenyReason"/>, sem dado pessoal) e a concessão que autorizou
/// quando permite (<see cref="GrantUsed"/>, para a trilha de auditoria saber a
/// fonte). O invariante — permitida tem <see cref="GrantUsed"/> e não
/// <see cref="DenyReason"/>; negada tem <see cref="DenyReason"/> e não
/// <see cref="GrantUsed"/> — é garantido <b>por construção</b>: não há caminho
/// público que produza uma decisão incoerente.
/// </summary>
public sealed record AuthorizationDecision
{
    /// <summary>Veredito da decisão.</summary>
    public bool Allowed { get; }

    /// <summary>Motivo da negativa — presente se, e somente se, negada.</summary>
    public DenyReason? DenyReason { get; }

    /// <summary>Concessão que autorizou — presente se, e somente se, permitida.</summary>
    public EffectiveGrant? GrantUsed { get; }

    /// <summary>
    /// O desfecho da decisão: permitido, impedido (negada porque o solicitante tem inscrição enviada no processo) ou
    /// negado. É calculado aqui, uma vez, para que o registro e a borda não o rederivem comparando o motivo.
    /// </summary>
    public ResultadoDoAcesso Resultado =>
        Allowed ? ResultadoDoAcesso.Permitido
        : DenyReason!.Codigo == MotivoNegativa.ImpedidoPorInscricaoNoProcesso ? ResultadoDoAcesso.Impedido
        : ResultadoDoAcesso.Negado;

    private AuthorizationDecision(bool allowed, DenyReason? denyReason, EffectiveGrant? grantUsed)
    {
        Allowed = allowed;
        DenyReason = denyReason;
        GrantUsed = grantUsed;
    }

    /// <summary>
    /// Constrói uma decisão <b>permitida</b>, registrando qual concessão (e qual
    /// fonte) autorizou. Rejeita <paramref name="grant"/> nulo: uma decisão
    /// permitida sem o <see cref="GrantUsed"/> que a sustenta é incoerente — a
    /// auditoria precisa da contraprova da concessão usada.
    /// </summary>
    public static AuthorizationDecision Permitir(EffectiveGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);

        return new AuthorizationDecision(allowed: true, denyReason: null, grantUsed: grant);
    }

    /// <summary>
    /// Constrói uma decisão <b>negada</b>, com o motivo estruturado do conjunto
    /// fechado. O código é validado por <see cref="Contracts.DenyReason.De"/>
    /// (rejeita valor fora do conjunto). A decisão negada nunca carrega
    /// <see cref="GrantUsed"/>.
    /// </summary>
    public static AuthorizationDecision Negar(MotivoNegativa codigo)
        => new(allowed: false, denyReason: Contracts.DenyReason.De(codigo), grantUsed: null);
}
