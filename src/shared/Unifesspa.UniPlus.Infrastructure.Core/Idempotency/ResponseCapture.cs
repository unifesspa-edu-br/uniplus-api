namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Desvia o corpo da resposta para a memória enquanto a action executa, para o filtro decidir se a guarda antes de devolvê-la
/// ao cliente. Ao descartar, o corpo original da resposta é restaurado.
/// </summary>
internal sealed class ResponseCapture : IAsyncDisposable
{
    private readonly HttpResponse _response;
    private readonly Stream _original;
    private readonly MemoryStream _captured = new();

    public ResponseCapture(HttpResponse response)
    {
        _response = response;
        _original = response.Body;
        response.Body = _captured;
    }

    public byte[] Content() => _captured.ToArray();

    public async Task ReturnToClientAsync(CancellationToken cancellationToken)
    {
        _captured.Position = 0;
        await _captured.CopyToAsync(_original, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        _response.Body = _original;
        await _captured.DisposeAsync().ConfigureAwait(false);
    }
}
