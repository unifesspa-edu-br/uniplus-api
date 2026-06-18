namespace Unifesspa.UniPlus.Geo.Infrastructure.Observability;

using System.Diagnostics.Metrics;

using Unifesspa.UniPlus.Infrastructure.Core.Observability;

/// <summary>
/// Métricas OpenTelemetry da carga do ETL DNE (Story #674, CA-07). O <see cref="Meter"/>
/// usa o nome canônico do serviço (<see cref="UniPlusServiceNames.Geo"/>), já coletado
/// por <c>AdicionarObservabilidade</c> via <c>AddMeter(nomeServico)</c> — sem registro
/// extra no Program. Sem PII (reference data público); as tags são versão e status.
/// </summary>
internal sealed class GeoEtlMetrics : IDisposable
{
    private readonly Meter _meter;
    private readonly Histogram<double> _duracao;
    private readonly Counter<long> _linhas;
    private readonly Counter<long> _degradados;
    private readonly Counter<long> _execucoes;

    public GeoEtlMetrics()
    {
        _meter = new Meter(UniPlusServiceNames.Geo);
        _duracao = _meter.CreateHistogram<double>("geo.etl.duracao", unit: "ms", description: "Duração da carga do ETL DNE.");
        _linhas = _meter.CreateCounter<long>("geo.etl.linhas", unit: "{linha}", description: "Linhas inseridas + atualizadas pela carga.");
        _degradados = _meter.CreateCounter<long>("geo.etl.degradados", unit: "{linha}", description: "Valores externos degradados para null no parse tolerante.");
        _execucoes = _meter.CreateCounter<long>("geo.etl.execucoes", unit: "{execucao}", description: "Execuções do ETL por status.");
    }

    /// <summary>Registra uma carga concluída com sucesso.</summary>
    public void RegistrarConclusao(string versao, double duracaoMs, long linhas, long degradados)
    {
        KeyValuePair<string, object?> tagVersao = new("versao", versao);
        KeyValuePair<string, object?> tagStatus = new("status", "concluida");
        _duracao.Record(duracaoMs, tagVersao, tagStatus);
        _linhas.Add(linhas, tagVersao);
        _degradados.Add(degradados, tagVersao);
        _execucoes.Add(1, tagVersao, tagStatus);
    }

    /// <summary>Registra uma carga que falhou.</summary>
    public void RegistrarFalha(string versao, double duracaoMs)
    {
        KeyValuePair<string, object?> tagVersao = new("versao", versao);
        KeyValuePair<string, object?> tagStatus = new("status", "falhou");
        _duracao.Record(duracaoMs, tagVersao, tagStatus);
        _execucoes.Add(1, tagVersao, tagStatus);
    }

    public void Dispose() => _meter.Dispose();
}
