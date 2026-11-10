namespace Unifesspa.UniPlus.Autorizacao.Application.Monitoramento;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;

/// <summary>Uma amostragem do estado da governança de perfis: lê o estado no banco e o entrega às métricas.</summary>
public sealed class AmostragemDaGovernanca
{
    private readonly ILeituraDoMonitoramento _leitura;
    private readonly MetricasDaGovernancaDePerfis _metricas;
    private readonly TimeProvider _relogio;

    public AmostragemDaGovernanca(ILeituraDoMonitoramento leitura, MetricasDaGovernancaDePerfis metricas, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(leitura);
        ArgumentNullException.ThrowIfNull(metricas);
        ArgumentNullException.ThrowIfNull(relogio);

        _leitura = leitura;
        _metricas = metricas;
        _relogio = relogio;
    }

    public async Task ExecutarAsync(CancellationToken cancellationToken) =>
        _metricas.AtualizarAmostra(await _leitura.LerAsync(_relogio.GetUtcNow(), cancellationToken).ConfigureAwait(false));
}
