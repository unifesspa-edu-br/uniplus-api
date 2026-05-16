namespace Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Marca entidades append-only de evidência forense per
/// <see href="../../../docs/adrs/0063-entidades-forensics-isentas-de-soft-delete.md">
/// ADR-0063</see>. Linhas de tabelas <see cref="IForensicEntity"/> só são
/// inseridas; qualquer <c>UPDATE</c>/<c>DELETE</c> em produção é tratado
/// como incidente operacional (corrupção de evidência).
/// </summary>
/// <remarks>
/// <para>
/// Entidades com este marcador são <strong>mutuamente exclusivas</strong>
/// com <see cref="Entities.EntityBase"/>: NÃO herdam <c>EntityBase</c> e
/// NÃO carregam <c>IsDeleted</c>/<c>DeletedAt</c>/<c>DeletedBy</c>. A regra
/// global "soft-delete em toda entidade" do projeto se aplica a entidades
/// de domínio mutáveis — não a evidência forense.
/// </para>
/// <para>
/// Fitness test em <c>tests/Unifesspa.UniPlus.ArchTests/Persistence/</c>
/// trava (a) herança de <c>EntityBase</c> em classe marcada, (b) ausência
/// do marcador em classes sem <c>EntityBase</c> persistidas, (c) ausência
/// de <c>sealed</c>/factory privada.
/// </para>
/// <para>
/// Aplicação inicial (Story #460): <c>ObrigatoriedadeLegalHistorico</c> e
/// <c>EditalGovernanceSnapshot</c> no módulo Seleção. Tabelas futuras
/// (<c>proprietario_historico</c>, <c>area_interesse_binding_historico</c>)
/// também implementam quando vierem.
/// </para>
/// </remarks>
public interface IForensicEntity
{
    /// <summary>Identificador único da linha (UUID v7 do timestamp do snapshot).</summary>
    Guid Id { get; }
}
