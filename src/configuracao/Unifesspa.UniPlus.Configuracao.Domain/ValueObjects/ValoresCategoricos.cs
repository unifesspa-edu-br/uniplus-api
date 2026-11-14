namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A conferência da lista de valores categóricos de um tipo de conceito.</summary>
internal static class ValoresCategoricos
{
    private const int MinimoDeValores = 2;

    /// <summary>Os valores válidos na ordem informada, ou <see langword="null"/> com as violações acumuladas em <paramref name="erros"/>.</summary>
    public static IReadOnlyList<ValorCategorico>? Conferir(IReadOnlyList<ValorInformado>? informados, List<FieldError> erros)
    {
        ArgumentNullException.ThrowIfNull(erros);
        int errosAntes = erros.Count;
        if (informados is not { Count: >= MinimoDeValores })
        {
            erros.Add(new("valores", new DomainError(
                TipoConceitoErrorCodes.ValoresCategoricosInsuficientes,
                $"O tipo de conceito categórico deve ter ao menos {MinimoDeValores} valores.")));
        }

        List<ValorCategorico?> conferidos = [.. (informados ?? []).Select((valor, indice) => ValorCategorico.Conferir(valor, indice, erros))];
        ConferirRepeticoes(conferidos, erros);
        return erros.Count == errosAntes ? [.. conferidos.OfType<ValorCategorico>()] : null;
    }

    private static void ConferirRepeticoes(List<ValorCategorico?> conferidos, List<FieldError> erros)
    {
        HashSet<string> codigos = new(StringComparer.Ordinal);
        HashSet<string> rotulos = new(StringComparer.Ordinal);
        for (int indice = 0; indice < conferidos.Count; indice++)
        {
            if (conferidos[indice] is not { } valor)
            {
                continue;
            }

            AcusarSeRepetido(codigos.Add(valor.Codigo), $"valores[{indice}].codigo", "código", erros);
            AcusarSeRepetido(rotulos.Add(valor.Rotulo), $"valores[{indice}].rotulo", "rótulo", erros);
        }
    }

    private static void AcusarSeRepetido(bool inedito, string caminho, string campo, List<FieldError> erros)
    {
        if (!inedito)
        {
            erros.Add(new(caminho, new DomainError(
                TipoConceitoErrorCodes.ValorCategoricoDuplicado,
                $"O {campo} do valor categórico já foi usado por outro valor do mesmo tipo.")));
        }
    }
}
