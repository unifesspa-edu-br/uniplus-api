namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O conjunto padrão de documentos que o modelo exige do processo (UNI-REQ-0026): os documentos avulsos e
/// os grupos de documentos. Todo lugar que percorre documentos passa por <see cref="ComCaminho"/>, para que
/// o documento de um grupo conte como o avulso.
/// </summary>
/// <param name="DocumentosExigidos">Os documentos exigidos avulsos.</param>
/// <param name="GruposDeDocumentos">Os grupos de documentos, cada um com a quantidade mínima a apresentar.</param>
public sealed record ConjuntoDeDocumentosDoModelo(
    IReadOnlyList<DocumentoExigidoDoModelo> DocumentosExigidos,
    IReadOnlyList<GrupoDeDocumentosDoModelo> GruposDeDocumentos)
{
    /// <summary>O caminho do conjunto no conteúdo do modelo.</summary>
    public const string Caminho = "conjuntoDeDocumentos";

    /// <summary>O modelo que não exige documento.</summary>
    public static ConjuntoDeDocumentosDoModelo Nenhum { get; } = new([], []);

    /// <summary>O caminho do documento avulso na posição dada, no conteúdo do modelo.</summary>
    public static string CaminhoDoDocumento(int documento) => $"{Caminho}.documentosExigidos[{documento}]";

    /// <summary>O caminho do grupo de documentos na posição dada, no conteúdo do modelo.</summary>
    public static string CaminhoDoGrupo(int grupo) => $"{Caminho}.gruposDeDocumentos[{grupo}]";

    /// <summary>O caminho do documento na posição dada do grupo, no conteúdo do modelo.</summary>
    public static string CaminhoDoDocumentoDoGrupo(int grupo, int documento) => $"{CaminhoDoGrupo(grupo)}.documentos[{documento}]";

    /// <summary>Cada documento, avulso ou de grupo, com o caminho dele no conteúdo do modelo.</summary>
    public IEnumerable<(string Caminho, DocumentoExigidoDoModelo Documento)> ComCaminho() =>
        (DocumentosExigidos ?? []).Select(static (documento, i) => (CaminhoDoDocumento(i), documento))
            .Concat((GruposDeDocumentos ?? []).SelectMany(static (grupo, g) =>
                grupo.Documentos.Select((documento, j) => (CaminhoDoDocumentoDoGrupo(g, j), documento))));

    /// <summary>Os fatos que as condições dos documentos citam, inclusive o das opções que os multiplicam.</summary>
    public IEnumerable<string> FatosCitados() => ComCaminho().SelectMany(static c => c.Documento.FatosCitados);

    /// <summary>A condição de cada documento, com o caminho do campo dela no conteúdo do modelo.</summary>
    public IEnumerable<PredicadoNaEntrada> Predicados() =>
        ComCaminho().SelectMany(static c => c.Documento.Predicados().Select(p => p with { Caminho = $"{c.Caminho}.{p.Caminho}" }));

    /// <summary>O conjunto com os códigos dos documentos na forma gravada, nas mesmas posições.</summary>
    public ConjuntoDeDocumentosDoModelo NaFormaGravada() => new(
        [.. (DocumentosExigidos ?? []).Select(DocumentoNaFormaGravada)],
        [.. (GruposDeDocumentos ?? []).Select(static g => g with { Documentos = [.. g.Documentos.Select(DocumentoNaFormaGravada)] })]);

    private static DocumentoExigidoDoModelo DocumentoNaFormaGravada(DocumentoExigidoDoModelo documento) => documento with
    {
        FaseCodigo = TextoGravavel.CodigoNaFormaGravada(documento.FaseCodigo),
        TipoDocumentoCodigo = TextoGravavel.CodigoNaFormaGravada(documento.TipoDocumentoCodigo),
        Requisitos = documento.Requisitos.NaFormaGravada(),
        PorOpcao = documento.PorOpcao is { } porOpcao
            ? new ExigenciaPorOpcao(
                TextoGravavel.CodigoNaFormaGravada(porOpcao.FatoCodigo), [.. porOpcao.Exceto.Select(static v => TextoGravavel.CodigoNaFormaGravada(v))])
            : null,
    };
}
