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
/// para que as recusas da conferência contra o catálogo apontem o que veio. Os documentos que a leitura aceitou em grupo
/// que ela recusou seguem só para a conferência no catálogo, e não fazem parte do conjunto.
/// </summary>
internal sealed record ConjuntoLidoDeDocumentos(
    IReadOnlyList<(int Indice, DocumentoExigidoDoModelo Documento)> DocumentosExigidos,
    IReadOnlyList<(int Indice, GrupoLidoDeDocumentos Grupo)> GruposDeDocumentos,
    IReadOnlyList<DocumentoDeGrupoNaoLido> DocumentosDeGruposNaoLidos)
{
    /// <summary>
    /// Cada documento lido ou de grupo recusado, com o caminho dele: os avulsos primeiro e depois os dos grupos, cada um
    /// na posição do pedido.
    /// </summary>
    public IEnumerable<(string Caminho, DocumentoExigidoDoModelo Documento)> ComCaminhoNaOrdemDoPedido() =>
        DocumentosExigidos.Select(static d => (Ordem: (0, d.Indice, 0), Caminho: ConjuntoDeDocumentosDoModelo.CaminhoDoDocumento(d.Indice), d.Documento))
            .Concat(GruposDeDocumentos.SelectMany(static g => g.Grupo.Documentos.Select(
                d => (Ordem: (1, g.Indice, d.Indice), Caminho: ConjuntoDeDocumentosDoModelo.CaminhoDoDocumentoDoGrupo(g.Indice, d.Indice), d.Documento))))
            .Concat(DocumentosDeGruposNaoLidos.Select(static d => (Ordem: (1, d.Grupo, d.Posicao), d.Caminho, d.Documento)))
            .OrderBy(static d => d.Ordem)
            .Select(static d => (d.Caminho, d.Documento));

    public ConjuntoDeDocumentosDoModelo ParaConjunto() => new(
        [.. DocumentosExigidos.Select(static d => d.Documento)],
        [.. GruposDeDocumentos.Select(static g => g.Grupo.Grupo)]);
}

/// <summary>Um documento lido de um grupo de documentos que a leitura recusou, com a posição do grupo e a do documento na entrada.</summary>
internal sealed record DocumentoDeGrupoNaoLido(int Grupo, int Posicao, DocumentoExigidoDoModelo Documento)
{
    public string Caminho => ConjuntoDeDocumentosDoModelo.CaminhoDoDocumentoDoGrupo(Grupo, Posicao);
}
