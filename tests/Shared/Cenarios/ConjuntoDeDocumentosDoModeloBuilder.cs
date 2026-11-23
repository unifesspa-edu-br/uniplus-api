namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// O conjunto de documentos de um modelo na forma de entrada dos testes: sem dizer nada, o modelo não exige
/// documento nem grupo. Cada método devolve um construtor novo, então um ponto de partida compartilhado não
/// vaza para os demais.
/// </summary>
internal sealed record ConjuntoDeDocumentosDoModeloBuilder
{
    private IReadOnlyList<DocumentoExigidoDoModeloInput> Avulsos { get; init; } = [];

    private IReadOnlyList<GrupoDeDocumentosDoModeloInput> Grupos { get; init; } = [];

    /// <summary>O modelo sem documento avulso nem grupo.</summary>
    public static ConjuntoDeDocumentosDoModeloBuilder Vazio() => new();

    /// <summary>O conjunto só com documentos avulsos.</summary>
    public static ConjuntoDeDocumentosDoModeloInput SoDocumentos(params DocumentoExigidoDoModeloInput[] documentos) =>
        Vazio().ComDocumentos(documentos).Construir();

    /// <summary>O grupo sem indeferimento, com a quantidade mínima e os documentos dados.</summary>
    public static GrupoDeDocumentosDoModeloInput Grupo(int? quantidadeMinima, params DocumentoExigidoDoModeloInput[] documentos) =>
        new(quantidadeMinima, null, documentos);

    /// <summary>O grupo com a consequência do indeferimento, sem bases legais.</summary>
    public static GrupoDeDocumentosDoModeloInput GrupoComIndeferimento(
        int? quantidadeMinima, string consequencia, params DocumentoExigidoDoModeloInput[] documentos) =>
        new(quantidadeMinima, new IndeferimentoDoGrupoDeDocumentosDoModeloInput(consequencia, []), documentos);

    /// <summary>Acrescenta documentos avulsos.</summary>
    public ConjuntoDeDocumentosDoModeloBuilder ComDocumentos(params DocumentoExigidoDoModeloInput[] documentos) =>
        this with { Avulsos = [.. Avulsos, .. documentos] };

    /// <summary>Acrescenta grupos de documentos.</summary>
    public ConjuntoDeDocumentosDoModeloBuilder ComGrupos(params GrupoDeDocumentosDoModeloInput[] grupos) =>
        this with { Grupos = [.. Grupos, .. grupos] };

    public ConjuntoDeDocumentosDoModeloInput Construir() => new(Avulsos, Grupos);
}
