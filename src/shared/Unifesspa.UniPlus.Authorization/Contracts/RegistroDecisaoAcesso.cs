namespace Unifesspa.UniPlus.Authorization.Contracts;

using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;

/// <summary>
/// Evento do registro operacional restrito: o que fica gravado sobre <b>um uso</b>
/// de permissão — ator, instante, origem, permissão exigida, recurso alvo,
/// resultado e, conforme o caso, o motivo da negativa ou a fonte da concessão
/// que autorizou (CA-07 de <c>uniplus-api#1197</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Não carrega identificador de token.</b> Nem <i>subject</i> nem <c>jti</c>
/// entram no registro: são identificadores duráveis de pessoa e de sessão, e log
/// não é lugar deles. A correlação com a requisição fica pelo
/// <see cref="RequestId"/>. O recurso entra por metadados (tipo e identificadores
/// de escopo), nunca por conteúdo.
/// </para>
/// <para>
/// Os valores de enumeração são gravados já no <b>valor canônico</b>
/// (<see cref="ValoresCanonicos"/>), e não no identificador C#: o registro é
/// lido por fora do processo e o contrato do vocabulário é o canônico.
/// </para>
/// </remarks>
public sealed record RegistroDecisaoAcesso
{
    /// <summary>Instante do acesso, em UTC — o mesmo do contexto da requisição.</summary>
    public DateTimeOffset Instante { get; }

    /// <summary>Correlação da requisição.</summary>
    public string RequestId { get; }

    /// <summary>Código da permissão exigida.</summary>
    public string Permissao { get; }

    /// <summary>Tipo do recurso alvo.</summary>
    public string RecursoTipo { get; }

    /// <summary>
    /// Sensibilidade efetiva do dado alcançado, em valor canônico: a maior entre
    /// a que a permissão classifica e a que o contexto do recurso declara — a
    /// mesma que decidiu a exigência de base legal. Registrar só a do contexto
    /// faria uma operação sensível constar como interna, e consultas de
    /// conformidade sobre o registro deixariam de encontrá-la.
    /// </summary>
    public string Sensibilidade { get; }

    /// <summary>Unidade proprietária do recurso, quando o escopo se aplica.</summary>
    public Guid? UnidadeProprietariaId { get; }

    /// <summary>Processo seletivo do recurso, quando o escopo se aplica.</summary>
    public Guid? ProcessoId { get; }

    /// <summary>Chamada do recurso, quando o escopo se aplica.</summary>
    public Guid? ChamadaId { get; }

    /// <summary>Canal pelo qual a requisição chegou, em valor canônico.</summary>
    public string Origem { get; }

    /// <summary>Veredito da decisão.</summary>
    public bool Permitido { get; }

    /// <summary>Motivo canônico da negativa — presente se, e somente se, negada.</summary>
    public string? MotivoNegativa { get; }

    /// <summary>Fonte canônica da concessão usada — presente se, e somente se, permitida.</summary>
    public string? FonteGrant { get; }

    /// <summary>Identificador da concessão usada, quando ela o possui.</summary>
    public Guid? GrantId { get; }

    private RegistroDecisaoAcesso(
        DateTimeOffset instante,
        string requestId,
        string permissao,
        string recursoTipo,
        string sensibilidade,
        Guid? unidadeProprietariaId,
        Guid? processoId,
        Guid? chamadaId,
        string origem,
        bool permitido,
        string? motivoNegativa,
        string? fonteGrant,
        Guid? grantId)
    {
        Instante = instante;
        RequestId = requestId;
        Permissao = permissao;
        RecursoTipo = recursoTipo;
        Sensibilidade = sensibilidade;
        UnidadeProprietariaId = unidadeProprietariaId;
        ProcessoId = processoId;
        ChamadaId = chamadaId;
        Origem = origem;
        Permitido = permitido;
        MotivoNegativa = motivoNegativa;
        FonteGrant = fonteGrant;
        GrantId = grantId;
    }

    /// <summary>
    /// Deriva o registro dos mesmos contratos que a decisão recebeu, mais a
    /// decisão tomada. É o único caminho de construção: a escolha do que entra —
    /// e, sobretudo, do que não entra — no registro fica em um lugar só, em vez
    /// de ser recomposta por cada chamador.
    /// </summary>
    public static RegistroDecisaoAcesso De(
        PermissionRequirement requirement,
        ResourceContext resource,
        AuthorizationRequestContext request,
        AuthorizationDecision decision)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(decision);

        EffectiveGrant? grant = decision.GrantUsed;

        return new RegistroDecisaoAcesso(
            request.DataAcesso,
            request.RequestId,
            requirement.Permissao,
            resource.RecursoTipo,
            ValoresCanonicos.De(MaisRestritiva(requirement.Sensibilidade, resource.Sensibilidade)),
            resource.UnidadeProprietariaId,
            resource.ProcessoId,
            resource.ChamadaId,
            ValoresCanonicos.De(request.Origem),
            decision.Allowed,
            decision.DenyReason is { } motivo ? ValoresCanonicos.De(motivo.Codigo) : null,
            grant is not null ? ValoresCanonicos.De(grant.Fonte) : null,
            grant?.GrantId);
    }

    // A escala de Sensibilidade é crescente por construção (Publica < Interna <
    // Pessoal < Sensivel), então a mais restritiva é a de maior valor.
    private static Sensibilidade MaisRestritiva(Sensibilidade primeira, Sensibilidade segunda)
        => (Sensibilidade)Math.Max((int)primeira, (int)segunda);
}
