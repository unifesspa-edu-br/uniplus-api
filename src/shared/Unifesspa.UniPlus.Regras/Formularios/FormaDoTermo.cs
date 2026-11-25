namespace Unifesspa.UniPlus.Regras.Formularios;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;

/// <summary>A forma de um termo exigido pelo formulário, a mesma no processo e no modelo.</summary>
public static class FormaDoTermo
{
    public const int CodigoMaxLength = 60;

    /// <summary>A forma do termo que não depende do catálogo: confere todo o payload antes de ler as versões.</summary>
    public static List<FieldError> ValidarFormaBasica(string? codigo, int ordem) =>
        [.. RecusasDoCodigo(codigo), .. RecusasDaOrdem(ordem)];

    private static List<FieldError> RecusasDoCodigo(string? codigo) =>
        RecusaDoCodigo(codigo) is { } recusa ? [new FieldError("codigo", recusa)] : [];

    private static DomainError? RecusaDoCodigo(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return new DomainError(TermoFormularioErrorCodes.CodigoObrigatorio, "O código do termo exigido é obrigatório.");
        }
        if (codigo.Length > CodigoMaxLength)
        {
            return new DomainError(TermoFormularioErrorCodes.CodigoTamanho, $"O código do termo exigido deve ter no máximo {CodigoMaxLength} caracteres.");
        }
        if (!CodigoValido(codigo))
        {
            return new DomainError(TermoFormularioErrorCodes.CodigoFormatoInvalido,
                "O código do termo exigido começa com letra maiúscula e tem só letras maiúsculas, algarismos e sublinhados isolados (ex.: CIENCIA_DO_EDITAL).");
        }
        return null;
    }

    private static bool CodigoValido([NotNullWhen(true)] string? codigo) =>
        !string.IsNullOrWhiteSpace(codigo) && codigo.Length <= CodigoMaxLength && FormatoDoCodigo.TemFormatoValido(codigo);

    private static IEnumerable<FieldError> RecusasDaOrdem(int ordem)
    {
        if (ordem < 0)
        {
            yield return new("ordem", new DomainError(TermoFormularioErrorCodes.OrdemInvalida, "A ordem do termo não pode ser negativa."));
        }
    }

    /// <summary>Confere a unicidade dos códigos válidos e das ordens dos termos.</summary>
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
        if (CodigoValido(codigo) && !codigos.Add(codigo))
        {
            yield return new($"termos[{indice}].codigo", new DomainError(
                TermoFormularioErrorCodes.CodigoDuplicado, "O código aparece em mais de um termo do formulário."));
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
