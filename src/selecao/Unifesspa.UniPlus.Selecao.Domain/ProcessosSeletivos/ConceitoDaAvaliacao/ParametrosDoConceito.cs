namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// A natureza do conceito com os parâmetros que ela exige: o numérico tem escala e nenhum valor, o categórico
/// tem valores e nenhuma escala.
/// </summary>
internal sealed record ParametrosDoConceito(NaturezaConceito Natureza, EscalaNumerica? Escala, IReadOnlyList<ValorCategorico> Valores)
{
    /// <summary>Os parâmetros copiados, ou <see langword="null"/> com as recusas acumuladas em <paramref name="erros"/>.</summary>
    internal static ParametrosDoConceito? Conferir(
        NaturezaConceito natureza, EscalaInformada? escala, IReadOnlyList<ValorCategoricoInformado?>? valores, List<FieldError> erros)
    {
        int errosAntes = erros.Count;
        ParametrosDoConceito? parametros = natureza == NaturezaConceito.Numerico
            ? DoNumerico(escala, valores, erros)
            : DoCategorico(escala, valores, erros);
        return erros.Count == errosAntes ? parametros : null;
    }

    private static ParametrosDoConceito? DoNumerico(
        EscalaInformada? escala, IReadOnlyList<ValorCategoricoInformado?>? valores, List<FieldError> erros)
    {
        if (valores is { Count: > 0 })
        {
            erros.Add(Incoerente("valores", "O conceito numérico não admite valores categóricos."));
        }

        if (escala is null)
        {
            erros.Add(Incoerente("escala", "O conceito numérico exige a escala."));
            return null;
        }

        EscalaNumerica? copia = EscalaNumerica.Conferir(escala, erros);
        return copia is null ? null : new(NaturezaConceito.Numerico, copia, []);
    }

    private static ParametrosDoConceito DoCategorico(
        EscalaInformada? escala, IReadOnlyList<ValorCategoricoInformado?>? valores, List<FieldError> erros)
    {
        if (escala is not null)
        {
            erros.Add(Incoerente("escala", "O conceito categórico não admite escala numérica."));
        }

        return new(NaturezaConceito.Categorico, null, ValoresDoConceito.Conferir(valores, erros));
    }

    private static FieldError Incoerente(string campo, string mensagem) =>
        new(campo, new DomainError(ConceitoDaEtapaErrorCodes.NaturezaIncoerente, mensagem));
}
