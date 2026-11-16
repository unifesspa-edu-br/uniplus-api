namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A conferência da lista de valores de um conceito categórico: ao menos dois, de código único.</summary>
internal static class ValoresDoConceito
{
    private const int MinimoDeValores = 2;

    /// <summary>
    /// Os valores copiados na ordem informada, somente leitura. A lista só é confiável se nenhuma recusa
    /// foi acumulada em <paramref name="erros"/>.
    /// </summary>
    internal static IReadOnlyList<ValorCategorico> Conferir(IReadOnlyList<ValorCategoricoInformado?>? informados, List<FieldError> erros)
    {
        if (informados is not { Count: >= MinimoDeValores })
        {
            erros.Add(new("valores", new DomainError(
                ConceitoDaEtapaErrorCodes.ValoresInsuficientes,
                $"O conceito categórico deve ter ao menos {MinimoDeValores} valores.")));
        }

        List<ValorCategorico.ValorLido> lidos = [.. (informados ?? []).Select((valor, indice) => ValorCategorico.Ler(valor, indice, erros))];
        AcusarRepetidos(lidos.Select(static valor => valor.Codigo), "codigo", ConceitoDaEtapaErrorCodes.ValorRepetido, "O código do valor já foi usado por outro valor do mesmo conceito.", erros);
        AcusarRepetidos(lidos.Select(static valor => valor.Rotulo), "rotulo", ConceitoDaEtapaErrorCodes.ValorRotuloRepetido, "O rótulo do valor já foi usado por outro valor do mesmo conceito.", erros);

        return lidos.Select(static valor => valor.Conferido()).OfType<ValorCategorico>().ToList().AsReadOnly();
    }

    private static void AcusarRepetidos(IEnumerable<string?> textos, string campo, string codigoDoErro, string mensagem, List<FieldError> erros)
    {
        HashSet<string> vistos = new(StringComparer.Ordinal);
        int indice = 0;
        foreach (string? texto in textos)
        {
            if (texto is not null && !vistos.Add(texto))
            {
                erros.Add(new($"valores[{indice}].{campo}", new DomainError(codigoDoErro, mensagem)));
            }

            indice++;
        }
    }
}
