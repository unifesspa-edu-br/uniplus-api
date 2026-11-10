namespace Unifesspa.UniPlus.Infrastructure.Core.Email;

/// <summary>Uma amostragem da fila durável de e-mail: lê o retrato no banco e o entrega às métricas.</summary>
public sealed class AmostragemDaFilaDeEmail
{
    private readonly LeituraDaFilaDeEmail _leitura;
    private readonly MetricasDaFilaDeEmail _metricas;

    public AmostragemDaFilaDeEmail(LeituraDaFilaDeEmail leitura, MetricasDaFilaDeEmail metricas)
    {
        ArgumentNullException.ThrowIfNull(leitura);
        ArgumentNullException.ThrowIfNull(metricas);

        _leitura = leitura;
        _metricas = metricas;
    }

    public async Task ExecutarAsync(CancellationToken cancellationToken) =>
        _metricas.AtualizarAmostra(await _leitura.LerAsync(cancellationToken).ConfigureAwait(false));
}
