namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;

/// <summary>O cabeçalho do formulário do processo: o título.</summary>
public static class FormaDoCabecalho
{
    public const int TituloMaxLength = 300;

    /// <summary>O título, quando há, cabe no limite.</summary>
    public static List<FieldError> ValidarTitulo(string? titulo) =>
        FormaDoItem.TextoOpcional(titulo) is { Length: > TituloMaxLength }
            ? [new("titulo", new DomainError(EstruturaFormularioErrorCodes.TituloTamanho,
                $"O título do formulário deve ter no máximo {TituloMaxLength} caracteres."))]
            : [];
}
