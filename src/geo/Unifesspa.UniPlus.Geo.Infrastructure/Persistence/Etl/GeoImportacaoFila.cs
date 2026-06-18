namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl;

using System.Threading.Channels;

/// <summary>
/// Fila in-process de disparos do ETL (Story #674): o endpoint admin / o seed
/// enfileiram o Id da execução já registrada; o <see cref="GeoImportacaoBackgroundService"/>
/// consome e roda a carga fora do request (resposta <c>202 Accepted</c>).
/// </summary>
/// <remarks>
/// Não é durável: uma execução enfileirada e não consumida por crash/restart é
/// reconciliada (marcada <c>Falhou</c>) no startup do worker — o índice único parcial
/// de "em andamento" garante que ela não bloqueie disparos futuros indefinidamente.
/// </remarks>
internal interface IGeoImportacaoFila
{
    /// <summary>Enfileira o Id de uma execução para processamento em segundo plano.</summary>
    ValueTask EnfileirarAsync(Guid execucaoId, CancellationToken cancellationToken);

    /// <summary>Sequência das execuções enfileiradas, em ordem de chegada.</summary>
    IAsyncEnumerable<Guid> LerAsync(CancellationToken cancellationToken);
}

/// <inheritdoc />
internal sealed class GeoImportacaoFila : IGeoImportacaoFila
{
    // Capacidade pequena: o índice único parcial garante ≤1 carga em andamento, então a
    // fila raramente passa de um item. FullMode.Wait dá backpressure se ainda assim encher.
    private readonly Channel<Guid> _canal = Channel.CreateBounded<Guid>(
        new BoundedChannelOptions(16)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });

    public ValueTask EnfileirarAsync(Guid execucaoId, CancellationToken cancellationToken) =>
        _canal.Writer.WriteAsync(execucaoId, cancellationToken);

    public IAsyncEnumerable<Guid> LerAsync(CancellationToken cancellationToken) =>
        _canal.Reader.ReadAllAsync(cancellationToken);
}
