namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Logging;

/// <summary>
/// Constantes que espelham o <c>derivedFields</c> configurado no datasource Loki
/// do Grafana (<c>uniplus-infra</c> PR #225). Single source of truth para a
/// regex consumida tanto por <see cref="TraceContextEnricherTests"/> quanto por
/// <see cref="SerilogConfigurationTests"/> — evita drift entre cópias dispersas
/// nos testes quando o datasource for atualizado.
/// </summary>
/// <remarks>
/// A verdade canônica desta regex vive no manifesto do datasource em
/// <c>uniplus-infra</c>. Esta constante é a contraparte C# que documenta a
/// invariante e permite asserções de contrato; atualizações lá devem propagar
/// aqui (e o teste <see cref="SerilogConfigurationTests"/>
/// <c>ConfigurarSerilog_ComActivityAtiva_DeveEmitirLogLineCasandoDerivedFieldsRegex</c>
/// falha cedo se o contrato regredir).
/// </remarks>
internal static class GrafanaDerivedFields
{
    /// <summary>
    /// Regex exata do <c>derivedFields</c> do datasource Loki — captura o
    /// <c>TraceId</c> hex32 lowercase no body do log entry para o
    /// link <em>"Ver trace no Tempo"</em>.
    /// </summary>
    public const string TraceIdMatcher = @"(?:traceID|trace_id|TraceId)[""]?[=:]\s*[""]?([a-fA-F0-9]{32})";
}
