namespace Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using System.Collections.Concurrent;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>
/// O acervo público para os testes que exercitam a divulgação pela fila sem armazenamento: guarda o
/// que a divulgação mandou copiar, em vez de copiar. Toda divulgação publica o documento do edital,
/// e esses testes medem a fila, não o armazenamento — os que medem o acervo usam o MinIO real.
/// </summary>
internal sealed class AcervoPublicoEmMemoria : IAcervoPublico
{
    private readonly ConcurrentDictionary<string, DocumentoNoAcervo> _copiados = new(StringComparer.Ordinal);

    /// <summary>As chaves no acervo, com o documento que cada uma recebeu.</summary>
    public IReadOnlyDictionary<string, DocumentoNoAcervo> Copiados => _copiados;

    public Task CopiarAsync(
        string chavePrivada,
        string chaveNoAcervo,
        DocumentoNoAcervo documento,
        CancellationToken cancellationToken = default)
    {
        _copiados.TryAdd(chaveNoAcervo, documento);
        return Task.CompletedTask;
    }
}
