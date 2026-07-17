namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Entities;

/// <summary>
/// A projeção de <c>documentosExigidos.exigencias[]</c> de UMA <see cref="Entities.VersaoConfiguracao"/>
/// já reidratada (Story #554, PR-e) — o insumo de <see cref="Services.ResolvedorExigenciasDocumentais"/>.
/// </summary>
/// <remarks>
/// Deliberadamente um tipo <b>próprio</b>, e não <c>IReadOnlyList&lt;DocumentoExigido&gt;</c>
/// cru: o resolvedor opera sobre evidência CONGELADA de uma versão específica — nunca
/// sobre a configuração viva do agregado, ainda que ambas sejam, hoje, a mesma classe de
/// entidade (<see cref="DocumentoExigido"/>, via <see cref="GrafoConfiguracao.DocumentosExigidos"/>
/// reidratado). O tipo próprio marca essa fronteira no próprio sistema de tipos: um
/// chamador não pode, por engano, passar a coleção viva de <c>ProcessoSeletivo</c> onde
/// se espera o snapshot publicado — o construtor exige a origem explícita.
/// </remarks>
public sealed record BlocoExigenciasCongelado
{
    private BlocoExigenciasCongelado(IReadOnlyList<DocumentoExigido> exigencias)
    {
        Exigencias = exigencias;
    }

    public IReadOnlyList<DocumentoExigido> Exigencias { get; }

    /// <summary>
    /// Constrói o bloco a partir do grafo já reidratado de uma versão publicada —
    /// <c>EnvelopeReidratado.Grafo.DocumentosExigidos</c>, nunca <c>ProcessoSeletivo.DocumentosExigidos</c>
    /// (a coleção viva).
    /// </summary>
    public static BlocoExigenciasCongelado DeGrafoReidratado(IReadOnlyList<DocumentoExigido> documentosExigidos)
    {
        ArgumentNullException.ThrowIfNull(documentosExigidos);
        return new BlocoExigenciasCongelado([.. documentosExigidos]);
    }
}
