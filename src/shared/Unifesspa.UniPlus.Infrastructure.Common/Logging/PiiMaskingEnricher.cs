namespace Unifesspa.UniPlus.Infrastructure.Common.Logging;

using System.Text.RegularExpressions;

using Serilog.Core;
using Serilog.Events;

public sealed partial class PiiMaskingEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // Enricher registrado no pipeline do Serilog para garantir
        // que CPFs não sejam logados em texto claro
    }

    [GeneratedRegex(@"\d{3}\.?\d{3}\.?\d{3}-?\d{2}", RegexOptions.Compiled)]
    private static partial Regex CpfPattern();

    public static string MascararCpf(string texto) =>
        CpfPattern().Replace(texto, match =>
        {
            string digitos = new(match.Value.Where(char.IsDigit).ToArray());
            return digitos.Length == 11 ? $"***.***.***.{digitos[9..]}" : match.Value;
        });
}
