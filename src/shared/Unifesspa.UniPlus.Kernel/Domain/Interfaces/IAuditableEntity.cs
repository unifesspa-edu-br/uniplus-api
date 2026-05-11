namespace Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Marca entidades de domínio que registram o usuário responsável por
/// criação e modificação como parte da auditoria de operações administrativas.
/// </summary>
/// <remarks>
/// <para>
/// Implementação é <b>opt-in</b>: aplicar somente em entidades cujas operações
/// têm semântica de autoria de domínio (avaliação, homologação, interposição
/// de recurso, decisão administrativa), não em entidades metadata
/// configuracional como <c>Edital</c>, <c>Cota</c> ou <c>ProcessoSeletivo</c>
/// — onde o registro estruturado em log (Serilog + <c>PiiMaskingEnricher</c>,
/// ADR-0011) já cobre o requisito da LGPD art. 37.
/// </para>
/// <para>
/// Quando uma entidade implementa esta interface, o
/// <c>AuditableInterceptor</c> (issue #390) preenche automaticamente
/// <see cref="CreatedBy"/> em <c>EntityState.Added</c> e <see cref="UpdatedBy"/>
/// em <c>EntityState.Modified</c> a partir do <c>IUserContext</c> resolvido
/// no escopo do request — fallback para <c>"system"</c> em fluxos sem
/// principal autenticado (jobs, migrations).
/// </para>
/// <para>
/// As propriedades expõem apenas getter; entidades implementadoras devem
/// declarar setters não-públicos (geralmente <c>private set;</c>) para
/// permitir que o EF Core/interceptor escreva via reflection sem expor
/// mutação direta no domínio.
/// </para>
/// </remarks>
public interface IAuditableEntity
{
    /// <summary>Instante de criação do registro (UTC).</summary>
    DateTimeOffset CreatedAt { get; }

    /// <summary>Instante da última modificação do registro (UTC), ou <see langword="null"/> se nunca foi modificado.</summary>
    DateTimeOffset? UpdatedAt { get; }

    /// <summary>Identificador (<c>sub</c> do JWT) do usuário responsável pela criação, ou <c>"system"</c> em fluxos sem principal autenticado.</summary>
    string? CreatedBy { get; }

    /// <summary>Identificador (<c>sub</c> do JWT) do usuário responsável pela última modificação, ou <c>"system"</c> em fluxos sem principal autenticado.</summary>
    string? UpdatedBy { get; }
}
