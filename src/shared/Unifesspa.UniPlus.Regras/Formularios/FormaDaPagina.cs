namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;

/// <summary>A forma de uma página do formulário, a mesma no processo e no modelo.</summary>
public static class FormaDaPagina
{
    public const int CodigoMaxLength = 60;
    public const int TituloMaxLength = 300;
    public const int TextoMaxLength = 2000;

    /// <summary>
    /// Código e título obrigatórios e limitados, ordem não negativa, descrição e aviso limitados, e
    /// exibição condicional só na seção — o bloco de sistema aparece sempre. O tipo nulo é o que não se leu: só a
    /// regra da exibição, que depende dele, deixa de ser conferida.
    /// </summary>
    public static List<FieldError> Conferir(
        string? codigo, int ordem, TipoPaginaFormulario? tipo, string? titulo, string? descricao, string? aviso, bool temExibicao) =>
    [
        .. RecusasDoCodigo(codigo),
        .. RecusasDaOrdem(ordem),
        .. RecusasDoTitulo(titulo),
        .. RecusasDaDescricao(descricao),
        .. RecusasDoAviso(aviso),
        .. RecusasDaExibicao(tipo, temExibicao),
    ];

    private static IEnumerable<FieldError> RecusasDoCodigo(string? codigo)
    {
        if (codigo is null || codigo.Length > CodigoMaxLength || !FormatoDoCodigo.TemFormatoValido(codigo))
        {
            yield return new("codigo", new DomainError(
                EstruturaFormularioErrorCodes.PaginaCodigoInvalido,
                $"O código da página é obrigatório, começa com letra maiúscula e tem só letras maiúsculas, algarismos e sublinhados isolados, até {CodigoMaxLength} caracteres."));
        }
    }

    private static IEnumerable<FieldError> RecusasDaOrdem(int ordem)
    {
        if (ordem < 0)
        {
            yield return new("ordem", new DomainError(EstruturaFormularioErrorCodes.PaginaOrdemInvalida, "A ordem da página não pode ser negativa."));
        }
    }

    private static IEnumerable<FieldError> RecusasDoTitulo(string? titulo)
    {
        if ((titulo?.Trim() ?? string.Empty).Length is 0 or > TituloMaxLength)
        {
            yield return new($"{PaginaDoFormularioInput.CaminhoDaApresentacao}.titulo", new DomainError(
                EstruturaFormularioErrorCodes.PaginaTituloInvalido,
                $"O título da página é obrigatório e tem no máximo {TituloMaxLength} caracteres."));
        }
    }

    private static IEnumerable<FieldError> RecusasDaDescricao(string? descricao)
    {
        if (FormaDoItem.TextoOpcional(descricao) is { Length: > TextoMaxLength })
        {
            yield return new($"{PaginaDoFormularioInput.CaminhoDaApresentacao}.descricao", new DomainError(
                EstruturaFormularioErrorCodes.PaginaTextoTamanho, $"A descrição da página tem no máximo {TextoMaxLength} caracteres."));
        }
    }

    private static IEnumerable<FieldError> RecusasDoAviso(string? aviso)
    {
        if (FormaDoItem.TextoOpcional(aviso) is { Length: > TextoMaxLength })
        {
            yield return new($"{PaginaDoFormularioInput.CaminhoDaApresentacao}.aviso", new DomainError(
                EstruturaFormularioErrorCodes.PaginaTextoTamanho, $"O aviso da página tem no máximo {TextoMaxLength} caracteres."));
        }
    }

    private static IEnumerable<FieldError> RecusasDaExibicao(TipoPaginaFormulario? tipo, bool temExibicao)
    {
        if (temExibicao && tipo is { } lido && lido != TipoPaginaFormulario.Secao)
        {
            yield return new("exibicao", new DomainError(
                EstruturaFormularioErrorCodes.ExibicaoForaDeSecao,
                "Só a seção tem exibição condicional; o bloco de sistema aparece sempre."));
        }
    }
}
