namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;

/// <summary>A forma de uma etapa do formulário, a mesma no processo e no modelo.</summary>
public static class FormaDaEtapa
{
    public const int CodigoMaxLength = 60;
    public const int TituloMaxLength = 300;
    public const int TextoMaxLength = 2000;

    /// <summary>
    /// Código e título obrigatórios e limitados, ordem não negativa, descrição e aviso limitados, e
    /// exibição condicional só na seção — o bloco de sistema aparece sempre.
    /// </summary>
    public static List<FieldError> Conferir(
        string? codigo, int ordem, TipoEtapaFormulario tipo, string? titulo, string? descricao, string? aviso, bool temExibicao) =>
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
        if ((codigo?.Trim() ?? string.Empty).Length is 0 or > CodigoMaxLength)
        {
            yield return new("codigo", new DomainError(
                EstruturaFormularioErrorCodes.EtapaCodigoInvalido,
                $"O código da etapa é obrigatório e tem no máximo {CodigoMaxLength} caracteres."));
        }
    }

    private static IEnumerable<FieldError> RecusasDaOrdem(int ordem)
    {
        if (ordem < 0)
        {
            yield return new("ordem", new DomainError(EstruturaFormularioErrorCodes.EtapaOrdemInvalida, "A ordem da etapa não pode ser negativa."));
        }
    }

    private static IEnumerable<FieldError> RecusasDoTitulo(string? titulo)
    {
        if ((titulo?.Trim() ?? string.Empty).Length is 0 or > TituloMaxLength)
        {
            yield return new($"{EtapaFormularioInput.CaminhoDaApresentacao}.titulo", new DomainError(
                EstruturaFormularioErrorCodes.EtapaTituloInvalido,
                $"O título da etapa é obrigatório e tem no máximo {TituloMaxLength} caracteres."));
        }
    }

    private static IEnumerable<FieldError> RecusasDaDescricao(string? descricao)
    {
        if (FormaDoItem.TextoOpcional(descricao) is { Length: > TextoMaxLength })
        {
            yield return new($"{EtapaFormularioInput.CaminhoDaApresentacao}.descricao", new DomainError(
                EstruturaFormularioErrorCodes.EtapaTextoTamanho, $"A descrição da etapa tem no máximo {TextoMaxLength} caracteres."));
        }
    }

    private static IEnumerable<FieldError> RecusasDoAviso(string? aviso)
    {
        if (FormaDoItem.TextoOpcional(aviso) is { Length: > TextoMaxLength })
        {
            yield return new($"{EtapaFormularioInput.CaminhoDaApresentacao}.aviso", new DomainError(
                EstruturaFormularioErrorCodes.EtapaTextoTamanho, $"O aviso da etapa tem no máximo {TextoMaxLength} caracteres."));
        }
    }

    private static IEnumerable<FieldError> RecusasDaExibicao(TipoEtapaFormulario tipo, bool temExibicao)
    {
        if (temExibicao && tipo != TipoEtapaFormulario.Secao)
        {
            yield return new("exibicao", new DomainError(
                EstruturaFormularioErrorCodes.ExibicaoForaDeSecao,
                "Só a seção tem exibição condicional; o bloco de sistema aparece sempre."));
        }
    }
}
