namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Desvia o corpo da resposta para a memória enquanto a action executa, para o filtro decidir se a guarda antes de devolvê-la
/// ao cliente. Ao descartar, o corpo original da resposta é restaurado.
/// </summary>
internal sealed class CapturaDaResposta : IAsyncDisposable
{
    private readonly HttpResponse _resposta;
    private readonly Stream _original;
    private readonly MemoryStream _captura = new();

    public CapturaDaResposta(HttpResponse resposta)
    {
        _resposta = resposta;
        _original = resposta.Body;
        resposta.Body = _captura;
    }

    public byte[] Conteudo() => _captura.ToArray();

    public async Task DevolverAoClienteAsync(CancellationToken cancellationToken)
    {
        _captura.Position = 0;
        await _captura.CopyToAsync(_original, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        _resposta.Body = _original;
        await _captura.DisposeAsync().ConfigureAwait(false);
    }
}
