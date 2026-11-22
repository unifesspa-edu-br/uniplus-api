namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>As exigências documentais do processo: os documentos exigidos e as regras de comprovação.</summary>
public sealed class ExigenciasDeclaradas
{
    public ExigenciasDeclaradas(IReadOnlyList<DocumentoExigido> documentosExigidos, IReadOnlyList<RegraDeComprovacao> regrasDeComprovacao)
    {
        ArgumentNullException.ThrowIfNull(documentosExigidos);
        ArgumentNullException.ThrowIfNull(regrasDeComprovacao);
        DocumentosExigidos = [.. documentosExigidos];
        RegrasDeComprovacao = [.. regrasDeComprovacao];
    }

    public IReadOnlyList<DocumentoExigido> DocumentosExigidos { get; }

    public IReadOnlyList<RegraDeComprovacao> RegrasDeComprovacao { get; }
}
