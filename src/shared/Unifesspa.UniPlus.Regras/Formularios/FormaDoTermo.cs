namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;

/// <summary>A forma de um termo exigido pelo formulário, a mesma no processo e no modelo.</summary>
public static class FormaDoTermo
{
    public const int CodigoMaxLength = 60;

    /// <summary>A forma do termo que não depende do catálogo: confere todo o payload antes de ler as versões.</summary>
    public static List<FieldError> ValidarFormaBasica(string? codigo, int ordem) =>
        [.. RecusasDoCodigo(codigo), .. RecusasDaOrdem(ordem)];

    private static IEnumerable<FieldError> RecusasDoCodigo(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            yield return new("codigo", new DomainError(TermoFormularioErrorCodes.CodigoObrigatorio, "O código do termo exigido é obrigatório."));
        }
        else if (codigo.Trim().Length > CodigoMaxLength)
        {
            yield return new("codigo", new DomainError(
                TermoFormularioErrorCodes.CodigoTamanho, $"O código do termo exigido deve ter no máximo {CodigoMaxLength} caracteres."));
        }
    }

    private static IEnumerable<FieldError> RecusasDaOrdem(int ordem)
    {
        if (ordem < 0)
        {
            yield return new("ordem", new DomainError(TermoFormularioErrorCodes.OrdemInvalida, "A ordem do termo não pode ser negativa."));
        }
    }

    /// <summary>O código em NFC; o que não se normaliza é comparado como veio, e a recusa dele é de quem guarda o texto.</summary>
    private static string EmNfc(string codigo) => TextoNormalizavel.TentarNormalizar(codigo, out string normalizado) ? normalizado : codigo;

    /// <summary>
    /// Código e ordem únicos entre os termos do formulário, com o código na forma que o termo guarda
    /// (aparado, em NFC). Termo ausente ou sem código não entra na conferência do código — a recusa
    /// dele é a de forma.
    /// </summary>
    public static List<FieldError> ConferirUnicidade(IReadOnlyList<(string? Codigo, int Ordem)?> termos)
    {
        ArgumentNullException.ThrowIfNull(termos);

        List<FieldError> erros = [];
        HashSet<string> codigos = new(StringComparer.Ordinal);
        HashSet<int> ordens = [];
        for (int i = 0; i < termos.Count; i++)
        {
            if (termos[i] is { } termo)
            {
                erros.AddRange([.. RecusasDeCodigoDuplicado(i, termo.Codigo, codigos), .. RecusasDeOrdemDuplicada(i, termo.Ordem, ordens)]);
            }
        }

        return erros;
    }

    private static IEnumerable<FieldError> RecusasDeCodigoDuplicado(int indice, string? codigo, HashSet<string> codigos)
    {
        if (!string.IsNullOrWhiteSpace(codigo) && !codigos.Add(EmNfc(codigo.Trim())))
        {
            yield return new($"termos[{indice}].codigo", new DomainError(
                TermoFormularioErrorCodes.CodigoDuplicado, $"O código '{codigo.Trim()}' aparece em mais de um termo do formulário."));
        }
    }

    private static IEnumerable<FieldError> RecusasDeOrdemDuplicada(int indice, int ordem, HashSet<int> ordens)
    {
        if (!ordens.Add(ordem))
        {
            yield return new($"termos[{indice}].ordem", new DomainError(
                TermoFormularioErrorCodes.OrdemDuplicada, "Dois termos do formulário têm a mesma ordem."));
        }
    }
}
