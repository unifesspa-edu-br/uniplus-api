namespace Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using System.Collections.Concurrent;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>
/// O acervo público para os testes que exercitam a cópia ao acervo pela fila sem armazenamento: guarda o que a cópia
/// mandou copiar, em vez de copiar. Toda publicação copia o documento do edital, e esses testes medem a fila, não o
/// armazenamento — os que medem o acervo usam o MinIO real.
/// </summary>
internal sealed class AcervoPublicoEmMemoria : IAcervoPublico
{
    private readonly ConcurrentDictionary<string, DocumentoNoAcervo> _copiados = new(StringComparer.Ordinal);

    private volatile bool _indisponivel;
    private int _falhas;

    /// <summary>Enquanto verdadeiro, a cópia falha como o armazenamento fora do ar: nada é guardado.</summary>
    public bool Indisponivel
    {
        get => _indisponivel;
        set => _indisponivel = value;
    }

    /// <summary>Quantas cópias falharam por indisponibilidade.</summary>
    public int Falhas => Volatile.Read(ref _falhas);

    /// <summary>As chaves no acervo, com o documento que cada uma recebeu.</summary>
    public IReadOnlyDictionary<string, DocumentoNoAcervo> Copiados => _copiados;

    public Task CopiarAsync(
        string chavePrivada,
        string chaveNoAcervo,
        DocumentoNoAcervo documento,
        CancellationToken cancellationToken = default)
    {
        if (_indisponivel)
        {
            Interlocked.Increment(ref _falhas);
            throw new AcervoPublicoIndisponivelException(
                "O acervo público de teste está indisponível.", new IOException("armazenamento fora do ar"));
        }

        _copiados.TryAdd(chaveNoAcervo, documento);
        return Task.CompletedTask;
    }
}
