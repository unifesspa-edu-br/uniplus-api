namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// Um grupo de documentos lido, com a posição de cada documento na entrada: o documento que não se leu
/// fica de fora, e a posição dos demais continua a que veio.
/// </summary>
internal sealed record GrupoLidoDeDocumentos(
    GrupoDeDocumentosDoModelo Grupo, IReadOnlyList<(int Indice, DocumentoExigidoDoModelo Documento)> Documentos);

/// <summary>
/// O conjunto de documentos lido da escrita, com a posição de cada documento e de cada grupo na entrada,
/// para que as recusas da conferência contra o catálogo apontem o que veio.
/// </summary>
internal sealed record ConjuntoLidoDeDocumentos(
    IReadOnlyList<(int Indice, DocumentoExigidoDoModelo Documento)> DocumentosExigidos,
    IReadOnlyList<(int Indice, GrupoLidoDeDocumentos Grupo)> GruposDeDocumentos)
{
    /// <summary>Cada documento lido, avulso ou de grupo, com o caminho que veio na entrada.</summary>
    public IEnumerable<(string Caminho, DocumentoExigidoDoModelo Documento)> ComCaminho() =>
        DocumentosExigidos.Select(static d => (ConjuntoDeDocumentosDoModelo.CaminhoDoDocumento(d.Indice), d.Documento))
            .Concat(GruposDeDocumentos.SelectMany(static g => g.Grupo.Documentos.Select(
                d => (ConjuntoDeDocumentosDoModelo.CaminhoDoDocumentoDoGrupo(g.Indice, d.Indice), d.Documento))));

    public ConjuntoDeDocumentosDoModelo ParaConjunto() => new(
        [.. DocumentosExigidos.Select(static d => d.Documento)],
        [.. GruposDeDocumentos.Select(static g => g.Grupo.Grupo)]);
}
