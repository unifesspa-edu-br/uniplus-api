namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;

/// <summary>O cabeçalho do formulário, o mesmo no processo e no modelo: a finalidade e o título.</summary>
public static class FormaDoCabecalho
{
    public const int TituloMaxLength = 300;

    /// <summary>A finalidade é uma das três do formulário, e o título, quando há, cabe no limite.</summary>
    public static List<FieldError> Validar(FinalidadeFormulario finalidade, string? titulo) =>
        [.. ValidarFinalidade(finalidade), .. ValidarTitulo(titulo)];

    /// <summary>A finalidade é inscrição, isenção de taxa ou habilitação.</summary>
    public static List<FieldError> ValidarFinalidade(FinalidadeFormulario finalidade) =>
        finalidade == FinalidadeFormulario.Nenhuma || !Enum.IsDefined(finalidade)
            ? [new("finalidade", new DomainError(EstruturaFormularioErrorCodes.FinalidadeInvalida,
                "A finalidade do formulário é inscrição, isenção de taxa ou habilitação."))]
            : [];

    /// <summary>O título, quando há, cabe no limite.</summary>
    public static List<FieldError> ValidarTitulo(string? titulo) =>
        FormaDoItem.TextoOpcional(titulo) is { Length: > TituloMaxLength }
            ? [new("titulo", new DomainError(EstruturaFormularioErrorCodes.TituloTamanho,
                $"O título do formulário deve ter no máximo {TituloMaxLength} caracteres."))]
            : [];
}
