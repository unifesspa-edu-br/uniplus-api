namespace Unifesspa.UniPlus.Host.IntegrationTests.Transacao;

using System.Collections.Concurrent;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>Acervo público de teste que só anota as cópias pedidas, sem armazenamento por trás.</summary>
internal sealed class AcervoPublicoSemArmazenamento : IAcervoPublico
{
    private readonly ConcurrentQueue<DocumentoNoAcervo> _copias = new();

    public Task CopiarAsync(
        string chavePrivada,
        string chaveNoAcervo,
        DocumentoNoAcervo documento,
        CancellationToken cancellationToken = default)
    {
        _copias.Enqueue(documento);

        return Task.CompletedTask;
    }

    /// <summary>As cópias já pedidas para os documentos do ato.</summary>
    public int CopiasDoAto(Guid atoId) => _copias.Count(c => c.AtoId == atoId);
}
