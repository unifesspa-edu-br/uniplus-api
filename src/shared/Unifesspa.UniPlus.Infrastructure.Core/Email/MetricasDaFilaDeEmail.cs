namespace Unifesspa.UniPlus.Infrastructure.Core.Email;

using System.Diagnostics.Metrics;

/// <summary>O que a amostragem leu da fila durável de e-mail e das mensagens mortas.</summary>
/// <param name="Prontas">Linhas na tabela da fila de envio.</param>
/// <param name="IdadeDaMaisAntiga">Há quanto tempo a pronta mais antiga foi inserida; zero com a fila vazia.</param>
/// <param name="Atrasadas">Prontas inseridas há mais do que o limite da fila: trabalho elegível que ninguém retirou.</param>
/// <param name="Mortas">Estoque de mensagens de e-mail na fila de mensagens mortas.</param>
public sealed record AmostraDaFilaDeEmail(long Prontas, TimeSpan IdadeDaMaisAntiga, long Atrasadas, long Mortas);

/// <summary>
/// As métricas do envio de e-mail: o que a entrega aceitou e falhou, o atraso entre a publicação do pedido e o aceite, os instantes do último aceite e da última falha
/// e o retrato da fila. Os instantes e o retrato são gravados com <see cref="Interlocked"/> e lidos na coleta; os dois
/// instantes começam em zero ("nunca"), de modo que a série existe desde a subida, e a partida nunca é lida como aceite.
/// </summary>
public sealed class MetricasDaFilaDeEmail : IDisposable
{
    public const string NomeDoMedidor = "Unifesspa.UniPlus.Email";

    private const string UnidadeSegundos = "s";
    private const double MilissegundosPorSegundo = 1000.0;

    private readonly Meter _medidor = new(NomeDoMedidor);
    private readonly Counter<long> _enviados;
    private readonly Counter<long> _falhas;
    private readonly Histogram<double> _atraso;
    private readonly TimeProvider _relogio;
    private long _ultimoEnvioEmMs;
    private long _ultimaFalhaEmMs;
    private long _prontas;
    private long _idadeDaMaisAntigaEmMs;
    private long _atrasadas;
    private long _mortas;
    private long _ultimaAmostraEmTicks;

    public MetricasDaFilaDeEmail(TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(relogio);

        _relogio = relogio;
        _ultimaAmostraEmTicks = relogio.GetUtcNow().UtcTicks;
        _enviados = _medidor.CreateCounter<long>("uniplus.email.enviados", description: "E-mails aceitos pelo relay");
        _falhas = _medidor.CreateCounter<long>("uniplus.email.falhas", description: "Falhas de envio, por tipo (transitória ou permanente)");
        _atraso = _medidor.CreateHistogram<double>(
            "uniplus.email.atraso", UnidadeSegundos, "Tempo entre a publicação do pedido de e-mail e o aceite do relay, novas tentativas incluídas");
        CriarGaugesDosInstantes();
        CriarGaugesDaFila();
    }

    private void CriarGaugesDosInstantes()
    {
        _medidor.CreateObservableGauge(
            "uniplus.email.ultimo_envio",
            () => Interlocked.Read(ref _ultimoEnvioEmMs) / MilissegundosPorSegundo,
            UnidadeSegundos,
            "Instante do último aceite do relay, em segundos desde a época Unix; 0 se nunca");
        _medidor.CreateObservableGauge(
            "uniplus.email.ultima_falha",
            () => Interlocked.Read(ref _ultimaFalhaEmMs) / MilissegundosPorSegundo,
            UnidadeSegundos,
            "Instante da última falha de envio, em segundos desde a época Unix; 0 se nunca");
        _medidor.CreateObservableGauge("uniplus.email.amostra.idade", ObservarIdadeDaAmostra, UnidadeSegundos, "Tempo desde a última amostragem da fila bem-sucedida");
    }

    private void CriarGaugesDaFila()
    {
        _medidor.CreateObservableGauge("uniplus.email.fila.prontas", () => Interlocked.Read(ref _prontas), description: "Linhas na fila de envio");
        _medidor.CreateObservableGauge(
            "uniplus.email.fila.idade_mais_antiga",
            () => Interlocked.Read(ref _idadeDaMaisAntigaEmMs) / MilissegundosPorSegundo,
            UnidadeSegundos,
            "Idade da pronta mais antiga");
        _medidor.CreateObservableGauge("uniplus.email.fila.atrasadas", () => Interlocked.Read(ref _atrasadas), description: "Prontas inseridas há mais de 5 minutos");
        _medidor.CreateObservableGauge("uniplus.email.mortas", () => Interlocked.Read(ref _mortas), description: "Mensagens de e-mail na fila de mensagens mortas");
    }

    /// <summary>O medidor das métricas, para quem precisa escutá-lo.</summary>
    public Meter Medidor => _medidor;

    /// <summary>Registra o aceite do relay; atraso negativo, de relógios divergentes entre quem publicou e quem enviou, conta como zero.</summary>
    public void RegistrarEnvio(DateTimeOffset instante, TimeSpan atraso)
    {
        Interlocked.Exchange(ref _ultimoEnvioEmMs, instante.ToUnixTimeMilliseconds());
        _enviados.Add(1);
        _atraso.Record(Math.Max(0, atraso.TotalSeconds));
    }

    public void RegistrarFalha(bool permanente, DateTimeOffset instante)
    {
        Interlocked.Exchange(ref _ultimaFalhaEmMs, instante.ToUnixTimeMilliseconds());
        _falhas.Add(1, new KeyValuePair<string, object?>("tipo", permanente ? "permanente" : "transitoria"));
    }

    public void AtualizarAmostra(AmostraDaFilaDeEmail amostra)
    {
        ArgumentNullException.ThrowIfNull(amostra);

        Interlocked.Exchange(ref _prontas, amostra.Prontas);
        Interlocked.Exchange(ref _idadeDaMaisAntigaEmMs, (long)amostra.IdadeDaMaisAntiga.TotalMilliseconds);
        Interlocked.Exchange(ref _atrasadas, amostra.Atrasadas);
        Interlocked.Exchange(ref _mortas, amostra.Mortas);
        Interlocked.Exchange(ref _ultimaAmostraEmTicks, _relogio.GetUtcNow().UtcTicks);
    }

    public void Dispose() => _medidor.Dispose();

    private double ObservarIdadeDaAmostra() =>
        Math.Max(0, (_relogio.GetUtcNow() - new DateTimeOffset(Interlocked.Read(ref _ultimaAmostraEmTicks), TimeSpan.Zero)).TotalSeconds);
}
