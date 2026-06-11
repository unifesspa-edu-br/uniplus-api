namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Snapshot append-only de governança (regras avaliadas) de um
/// <see cref="Edital"/> no momento de <c>Edital.Publicar()</c>. Suporta a
/// evidência forense exigida por mandados de segurança e processos
/// administrativos — "quais regras se aplicaram quando este edital foi
/// publicado" (ADR-0058 §"Snapshot-on-bind").
/// </summary>
/// <remarks>
/// <para>
/// Implementa <see cref="IForensicEntity"/> per ADR-0063: forensic
/// append-only, deliberadamente sem <see cref="Kernel.Domain.Entities.EntityBase"/>
/// e sem soft-delete. Qualquer <c>UPDATE</c>/<c>DELETE</c> em produção é
/// tratado como incidente operacional.
/// </para>
/// <para>
/// <strong>Esta Story #460 cria apenas o schema vazio</strong> (CA-04). A
/// inserção de uma linha pelo agregado <c>Edital.Publicar()</c> — leitura das
/// regras vigentes + cópia para esta tabela — é responsabilidade da Story
/// #462 (US-F4-04). Fronteira deliberada para manter "1 PR por Story" do
/// Uni+.
/// </para>
/// <para>
/// <see cref="RegrasJson"/> guarda o array JSON com cada
/// <c>(rule_hash, base_legal, portaria_interna, descricao, vigencia,
/// predicado)</c> avaliada — independente de qualquer mutação posterior em
/// <c>obrigatoriedades_legais</c>, esta linha fica imutável.
/// </para>
/// </remarks>
public sealed class EditalGovernanceSnapshot : IForensicEntity
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid EditalId { get; private init; }

    public string RegrasJson { get; private init; } = null!;

    public DateTimeOffset SnapshottedAt { get; private init; }

    // Construtor de materialização do EF Core.
    private EditalGovernanceSnapshot()
    {
    }

    /// <summary>
    /// Cria um snapshot. <strong>Não consumido em #460</strong> — exposto
    /// para que #462 (US-F4-04) chame esta factory dentro de
    /// <c>Edital.Publicar()</c>.
    /// </summary>
    public static EditalGovernanceSnapshot Capturar(
        Guid editalId,
        string regrasJson,
        DateTimeOffset snapshottedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regrasJson);
        if (editalId == Guid.Empty)
        {
            throw new ArgumentException(
                "editalId não pode ser Guid.Empty.",
                nameof(editalId));
        }

        return new EditalGovernanceSnapshot
        {
            EditalId = editalId,
            RegrasJson = regrasJson,
            SnapshottedAt = snapshottedAt,
        };
    }
}
