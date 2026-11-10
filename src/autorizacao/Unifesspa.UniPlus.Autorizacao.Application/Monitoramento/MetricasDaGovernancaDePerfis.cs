namespace Unifesspa.UniPlus.Autorizacao.Application.Monitoramento;

using System.Diagnostics.Metrics;

/// <summary>O que a amostragem leu do estado da governança de perfis.</summary>
/// <param name="VersoesVencendo">Versões pendentes que expiram nas próximas 48 horas.</param>
/// <param name="JanelasAbertas">Janelas de migração efetivamente abertas.</param>
/// <param name="ReclassificacoesPendentes">Reclassificações sem a liberação de todas as etapas exigidas.</param>
/// <param name="Divergencias">Divergências registradas na conferência do catálogo em vigor.</param>
public sealed record AmostraDaGovernanca(long VersoesVencendo, long JanelasAbertas, long ReclassificacoesPendentes, long Divergencias);

/// <summary>
/// As métricas da governança de perfis: o retrato que a amostragem lê do banco, guardado com <see cref="Interlocked"/> e
/// lido na coleta, e o contador dos avisos que ficaram sem destinatário. A idade da amostra torna visível a amostragem
/// que parou: ela cresce desde a subida até a primeira amostra bem-sucedida e entre elas.
/// </summary>
public sealed class MetricasDaGovernancaDePerfis : IDisposable
{
    public const string NomeDoMedidor = "Unifesspa.UniPlus.Autorizacao";

    private readonly Meter _medidor = new(NomeDoMedidor);
    private readonly Counter<long> _avisosSemDestinatario;
    private readonly TimeProvider _relogio;
    private long _versoesVencendo;
    private long _janelasAbertas;
    private long _reclassificacoesPendentes;
    private long _divergencias;
    private long _ultimaAmostraEmTicks;

    public MetricasDaGovernancaDePerfis(TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(relogio);

        _relogio = relogio;
        _ultimaAmostraEmTicks = relogio.GetUtcNow().UtcTicks;
        _avisosSemDestinatario = _medidor.CreateCounter<long>(
            "uniplus.autorizacao.avisos_sem_destinatario", description: "Avisos da governança que ficaram sem destinatário, por tipo");
        CriarGauges();
    }

    /// <summary>O medidor das métricas, para quem precisa escutá-lo.</summary>
    public Meter Medidor => _medidor;

    public void AvisoSemDestinatario(string tipo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tipo);

        _avisosSemDestinatario.Add(1, new KeyValuePair<string, object?>("tipo", tipo));
    }

    public void AtualizarAmostra(AmostraDaGovernanca amostra)
    {
        ArgumentNullException.ThrowIfNull(amostra);

        Interlocked.Exchange(ref _versoesVencendo, amostra.VersoesVencendo);
        Interlocked.Exchange(ref _janelasAbertas, amostra.JanelasAbertas);
        Interlocked.Exchange(ref _reclassificacoesPendentes, amostra.ReclassificacoesPendentes);
        Interlocked.Exchange(ref _divergencias, amostra.Divergencias);
        Interlocked.Exchange(ref _ultimaAmostraEmTicks, _relogio.GetUtcNow().UtcTicks);
    }

    public void Dispose() => _medidor.Dispose();

    private void CriarGauges()
    {
        _medidor.CreateObservableGauge(
            "uniplus.autorizacao.versoes_pendentes.vencendo", () => Interlocked.Read(ref _versoesVencendo), description: "Versões pendentes que expiram nas próximas 48 horas");
        _medidor.CreateObservableGauge(
            "uniplus.autorizacao.janelas_abertas", () => Interlocked.Read(ref _janelasAbertas), description: "Janelas de migração efetivamente abertas");
        _medidor.CreateObservableGauge(
            "uniplus.autorizacao.reclassificacoes_pendentes", () => Interlocked.Read(ref _reclassificacoesPendentes), description: "Reclassificações sem a liberação de todas as etapas exigidas");
        _medidor.CreateObservableGauge(
            "uniplus.autorizacao.divergencias_da_governanca", () => Interlocked.Read(ref _divergencias), description: "Divergências da conferência do catálogo em vigor");
        _medidor.CreateObservableGauge("uniplus.autorizacao.amostra.idade", ObservarIdadeDaAmostra, "s", "Tempo desde a última amostragem bem-sucedida");
    }

    private double ObservarIdadeDaAmostra() =>
        Math.Max(0, (_relogio.GetUtcNow() - new DateTimeOffset(Interlocked.Read(ref _ultimaAmostraEmTicks), TimeSpan.Zero)).TotalSeconds);
}
