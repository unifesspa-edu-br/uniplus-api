namespace Unifesspa.UniPlus.Host.IntegrationTests.Email;

using System.Collections.Concurrent;

using OpenTelemetry;
using OpenTelemetry.Metrics;

/// <summary>Uma medida exportada pelo pipeline de métricas, com as etiquetas dela.</summary>
internal sealed record MedidaExportada(string Instrumento, double Valor, IReadOnlyDictionary<string, object?> Etiquetas);

/// <summary>
/// Exportador em memória do pipeline OpenTelemetry real: guarda as medidas de cada exportação, copiadas na hora, porque
/// o SDK reaproveita os objetos de métrica entre exportações.
/// </summary>
internal sealed class ExportadorDeMetricasEmMemoria : BaseExporter<Metric>
{
    private ConcurrentQueue<MedidaExportada> _ultimaExportacao = new();

    public IReadOnlyList<MedidaExportada> UltimaExportacao => [.. _ultimaExportacao];

    public override ExportResult Export(in Batch<Metric> batch)
    {
        ConcurrentQueue<MedidaExportada> medidas = new();
        foreach (Metric metrica in batch)
        {
            foreach (ref readonly MetricPoint ponto in metrica.GetMetricPoints())
            {
                medidas.Enqueue(new MedidaExportada(metrica.Name, ValorDe(metrica.MetricType, in ponto), EtiquetasDe(in ponto)));
            }
        }

        _ultimaExportacao = medidas;

        return ExportResult.Success;
    }

    private static double ValorDe(MetricType tipo, in MetricPoint ponto) => tipo switch
    {
        MetricType.LongSum => ponto.GetSumLong(),
        MetricType.DoubleSum => ponto.GetSumDouble(),
        MetricType.LongGauge => ponto.GetGaugeLastValueLong(),
        MetricType.DoubleGauge => ponto.GetGaugeLastValueDouble(),
        _ => throw new NotSupportedException($"Tipo de métrica {tipo} fora do que o teste lê."),
    };

    private static Dictionary<string, object?> EtiquetasDe(in MetricPoint ponto)
    {
        Dictionary<string, object?> etiquetas = [];
        foreach (KeyValuePair<string, object?> etiqueta in ponto.Tags)
        {
            etiquetas[etiqueta.Key] = etiqueta.Value;
        }

        return etiquetas;
    }
}
