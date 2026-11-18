namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>As exigências documentais do processo: os documentos exigidos e os nós da árvore de satisfação.</summary>
public sealed class ExigenciasDeclaradas
{
    public ExigenciasDeclaradas(IReadOnlyList<DocumentoExigido> documentosExigidos, IReadOnlyList<NoExigencia> nosExigencia)
    {
        ArgumentNullException.ThrowIfNull(documentosExigidos);
        ArgumentNullException.ThrowIfNull(nosExigencia);
        DocumentosExigidos = [.. documentosExigidos];
        NosExigencia = [.. nosExigencia];
    }

    public IReadOnlyList<DocumentoExigido> DocumentosExigidos { get; }

    public IReadOnlyList<NoExigencia> NosExigencia { get; }
}
