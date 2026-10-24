namespace Unifesspa.UniPlus.Verificador.Observabilidade;

using System.Diagnostics.Metrics;

using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>De onde veio a falha, para separar o armazenamento do antivírus.</summary>
public enum OrigemDoErro
{
    Minio,
    Clamd,
}

/// <summary>Os veredictos por tipo, os erros por origem e a idade da base de assinaturas.</summary>
public sealed class MetricasDoVerificador : IDisposable
{
    public const string NomeDoMedidor = "Unifesspa.UniPlus.Verificador";

    private readonly Meter _medidor = new(NomeDoMedidor);
    private readonly Counter<long> _veredictos;
    private readonly Counter<long> _erros;
    private readonly TimeProvider _relogio;
    private long _dataDaBaseEmTicks;

    public MetricasDoVerificador(TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(relogio);

        _relogio = relogio;
        _veredictos = _medidor.CreateCounter<long>("uniplus.verificador.veredictos", description: "Veredictos devolvidos, por tipo e motivo");
        _erros = _medidor.CreateCounter<long>("uniplus.verificador.erros", description: "Falhas da verificação, por origem");
        _medidor.CreateObservableGauge(
            "uniplus.verificador.base_antivirus.idade_dias",
            ObservarIdadeDaBase,
            unit: "d",
            description: "Idade da base de assinaturas em uso, lida do VERSION do clamd");
    }

    public void RegistrarVeredicto(VeredictoDaVerificacao veredicto, MotivoDaRecusa motivo) =>
        _veredictos.Add(1, new KeyValuePair<string, object?>("veredicto", veredicto.ToString()), new KeyValuePair<string, object?>("motivo", motivo.ToString()));

    public void RegistrarErro(OrigemDoErro origem) =>
        _erros.Add(1, new KeyValuePair<string, object?>("origem", origem == OrigemDoErro.Minio ? "minio" : "clamd"));

    public void AtualizarDataDaBase(DateTimeOffset dataDaBase) =>
        Interlocked.Exchange(ref _dataDaBaseEmTicks, dataDaBase.UtcTicks);

    public void Dispose() => _medidor.Dispose();

    private IEnumerable<Measurement<double>> ObservarIdadeDaBase()
    {
        long ticks = Interlocked.Read(ref _dataDaBaseEmTicks);
        if (ticks == 0)
        {
            return [];
        }

        double dias = (_relogio.GetUtcNow() - new DateTimeOffset(ticks, TimeSpan.Zero)).TotalDays;
        return [new Measurement<double>(Math.Max(0, dias))];
    }
}
