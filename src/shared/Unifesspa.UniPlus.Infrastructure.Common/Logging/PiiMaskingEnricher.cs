namespace Unifesspa.UniPlus.Infrastructure.Common.Logging;

using System.Collections.Generic;
using System.Text.RegularExpressions;

using Serilog.Core;
using Serilog.Events;

public sealed partial class PiiMaskingEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        foreach (KeyValuePair<string, LogEventPropertyValue> propriedade in logEvent.Properties.ToArray())
        {
            LogEventPropertyValue mascarado = MascararValor(propriedade.Value);
            if (!ReferenceEquals(mascarado, propriedade.Value))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(propriedade.Key, mascarado));
            }
        }
    }

    public static string MascararCpf(string texto)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return texto;
        }

        return CpfPattern().Replace(texto, static match =>
            string.Concat("***.***.***-".AsSpan(), match.ValueSpan[^2..]));
    }

    private static LogEventPropertyValue MascararValor(LogEventPropertyValue valor) => valor switch
    {
        ScalarValue scalar when scalar.Value is string texto => MascararScalar(scalar, texto),
        StructureValue estrutura => MascararEstrutura(estrutura),
        SequenceValue sequencia => MascararSequencia(sequencia),
        DictionaryValue dicionario => MascararDicionario(dicionario),
        _ => valor,
    };

    private static ScalarValue MascararScalar(ScalarValue original, string texto)
    {
        string mascarado = MascararCpf(texto);
        return ReferenceEquals(mascarado, texto) ? original : new ScalarValue(mascarado);
    }

    private static StructureValue MascararEstrutura(StructureValue original)
    {
        LogEventProperty[]? propriedadesMascaradas = null;

        for (int i = 0; i < original.Properties.Count; i++)
        {
            LogEventProperty propriedadeOriginal = original.Properties[i];
            LogEventPropertyValue valorMascarado = MascararValor(propriedadeOriginal.Value);

            if (!ReferenceEquals(valorMascarado, propriedadeOriginal.Value))
            {
                if (propriedadesMascaradas is null)
                {
                    propriedadesMascaradas = new LogEventProperty[original.Properties.Count];
                    for (int j = 0; j < i; j++)
                    {
                        propriedadesMascaradas[j] = original.Properties[j];
                    }
                }

                propriedadesMascaradas[i] = new LogEventProperty(propriedadeOriginal.Name, valorMascarado);
            }
            else if (propriedadesMascaradas is not null)
            {
                propriedadesMascaradas[i] = propriedadeOriginal;
            }
        }

        return propriedadesMascaradas is null
            ? original
            : new StructureValue(propriedadesMascaradas, original.TypeTag);
    }

    private static SequenceValue MascararSequencia(SequenceValue original)
    {
        LogEventPropertyValue[]? elementosMascarados = null;

        for (int i = 0; i < original.Elements.Count; i++)
        {
            LogEventPropertyValue elementoOriginal = original.Elements[i];
            LogEventPropertyValue valorMascarado = MascararValor(elementoOriginal);

            if (!ReferenceEquals(valorMascarado, elementoOriginal))
            {
                if (elementosMascarados is null)
                {
                    elementosMascarados = new LogEventPropertyValue[original.Elements.Count];
                    for (int j = 0; j < i; j++)
                    {
                        elementosMascarados[j] = original.Elements[j];
                    }
                }

                elementosMascarados[i] = valorMascarado;
            }
            else if (elementosMascarados is not null)
            {
                elementosMascarados[i] = elementoOriginal;
            }
        }

        return elementosMascarados is null ? original : new SequenceValue(elementosMascarados);
    }

    private static DictionaryValue MascararDicionario(DictionaryValue original)
    {
        KeyValuePair<ScalarValue, LogEventPropertyValue>[]? entradasMascaradas = null;
        int indice = 0;

        foreach (KeyValuePair<ScalarValue, LogEventPropertyValue> entrada in original.Elements)
        {
            ScalarValue chaveMascarada = entrada.Key.Value is string textoChave
                ? MascararScalar(entrada.Key, textoChave)
                : entrada.Key;
            LogEventPropertyValue valorMascarado = MascararValor(entrada.Value);

            bool chaveAlterada = !ReferenceEquals(chaveMascarada, entrada.Key);
            bool valorAlterado = !ReferenceEquals(valorMascarado, entrada.Value);

            if (chaveAlterada || valorAlterado)
            {
                if (entradasMascaradas is null)
                {
                    entradasMascaradas = new KeyValuePair<ScalarValue, LogEventPropertyValue>[original.Elements.Count];
                    int j = 0;
                    foreach (KeyValuePair<ScalarValue, LogEventPropertyValue> preservada in original.Elements)
                    {
                        if (j == indice)
                        {
                            break;
                        }

                        entradasMascaradas[j++] = preservada;
                    }
                }

                entradasMascaradas[indice] = new KeyValuePair<ScalarValue, LogEventPropertyValue>(chaveMascarada, valorMascarado);
            }
            else if (entradasMascaradas is not null)
            {
                entradasMascaradas[indice] = entrada;
            }

            indice++;
        }

        return entradasMascaradas is null ? original : new DictionaryValue(entradasMascaradas);
    }

    // Word boundaries ((?<!\d) / (?!\d)) impedem casar sub-sequências de 11 dígitos
    // dentro de números maiores (timestamps, IDs), evitando tanto falsos positivos
    // (log corruption) quanto falsos negativos onde dígitos do CPF vazariam fora do match.
    [GeneratedRegex(@"(?<![0-9])[0-9]{3}\.?[0-9]{3}\.?[0-9]{3}-?[0-9]{2}(?![0-9])")]
    private static partial Regex CpfPattern();
}
