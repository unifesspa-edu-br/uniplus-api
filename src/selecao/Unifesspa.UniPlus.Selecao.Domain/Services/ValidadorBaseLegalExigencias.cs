namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Entities;

using Enums;

/// <summary>
/// Domain service estático (Story #554, PR #898, issue #549, ADR-0074) — 5º item de
/// <see cref="ProcessoSeletivo.AvaliarConformidade"/>: para toda exigência que
/// <see cref="DocumentoExigido.DeterminaResultado"/>, exige ao menos uma
/// <see cref="DocumentoExigidoBaseLegal"/> com <see cref="StatusBaseLegal.Resolvido"/>, de
/// qualquer <see cref="TipoAbrangencia"/> — <c>InternaEdital</c> conta sozinha. Puro, sem
/// I/O; a validação em runtime avalia a versão CONGELADA (ADR-0070) — este service só
/// avalia o checklist estrutural na publicação/retificação.
/// </summary>
public static class ValidadorBaseLegalExigencias
{
    /// <summary>
    /// Semântica vazia (vacuous truth): um processo sem nenhuma exigência que determina
    /// resultado passa trivialmente — ausência de exigências não é, por si, pendência.
    /// </summary>
    public static bool TodasResolvidas(IReadOnlyCollection<DocumentoExigido> exigencias)
    {
        ArgumentNullException.ThrowIfNull(exigencias);

        return exigencias
            .Where(static e => e.DeterminaResultado())
            .All(static e => e.BasesLegais.Any(static b => b.Status == StatusBaseLegal.Resolvido));
    }
}
