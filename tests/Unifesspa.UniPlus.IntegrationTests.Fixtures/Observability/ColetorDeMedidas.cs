namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Observability;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

/// <summary>Uma medida colhida de um instrumento, com as etiquetas dela.</summary>
public sealed record MedidaColhida(string Instrumento, double Valor, IReadOnlyDictionary<string, object?> Etiquetas);

/// <summary>
/// Escuta uma instância de medidor pelo <see cref="MeterListener"/>: os contadores acumulam as medidas conforme chegam,
/// e os gauges observáveis são lidos quando o teste pede a coleta. Outras instâncias do mesmo medidor, de outros testes
/// da mesma execução, ficam de fora.
/// </summary>
public sealed class ColetorDeMedidas : IDisposable
{
    private readonly MeterListener _escuta = new();
    private readonly ConcurrentQueue<MedidaColhida> _acumuladas = new();
    private ConcurrentQueue<MedidaColhida> _observadas = new();

    public ColetorDeMedidas(Meter medidor)
    {
        ArgumentNullException.ThrowIfNull(medidor);

        _escuta.InstrumentPublished = (instrumento, escuta) =>
        {
            if (ReferenceEquals(instrumento.Meter, medidor))
            {
                escuta.EnableMeasurementEvents(instrumento);
            }
        };
        _escuta.SetMeasurementEventCallback<long>(Registrar);
        _escuta.SetMeasurementEventCallback<double>(Registrar);
        _escuta.SetMeasurementEventCallback<int>(Registrar);
        _escuta.Start();
    }

    /// <summary>Lê agora os gauges observáveis e devolve as medidas do instrumento, na ordem em que chegaram.</summary>
    public IReadOnlyList<MedidaColhida> Coletar(string instrumento)
    {
        _observadas = new ConcurrentQueue<MedidaColhida>();
        _escuta.RecordObservableInstruments();

        return [.. _observadas.Where(m => m.Instrumento == instrumento)];
    }

    /// <summary>A última medida do instrumento depois de uma coleta.</summary>
    public double Ultima(string instrumento) => Coletar(instrumento)[^1].Valor;

    /// <summary>A soma das medidas de um contador, com a etiqueta pedida quando há.</summary>
    public double Soma(string instrumento, string? etiqueta = null, object? valor = null) =>
        _acumuladas
            .Where(m => m.Instrumento == instrumento && (etiqueta is null || Equals(m.Etiquetas.GetValueOrDefault(etiqueta), valor)))
            .Sum(static m => m.Valor);

    public void Dispose() => _escuta.Dispose();

    private void Registrar<T>(Instrument instrumento, T medida, ReadOnlySpan<KeyValuePair<string, object?>> etiquetas, object? estado)
        where T : struct
    {
        MedidaColhida colhida = new(
            instrumento.Name, Convert.ToDouble(medida, System.Globalization.CultureInfo.InvariantCulture), etiquetas.ToArray().ToDictionary(e => e.Key, e => e.Value));

        (instrumento.IsObservable ? _observadas : _acumuladas).Enqueue(colhida);
    }
}
