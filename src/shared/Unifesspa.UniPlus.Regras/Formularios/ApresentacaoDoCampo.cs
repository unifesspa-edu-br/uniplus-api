namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;

/// <summary>
/// Como o campo se apresenta ao candidato: o rótulo, o tipo de campo, a ajuda e o pedido de
/// confirmação da resposta. É da configuração do formulário, não do fato: o mesmo fato pode ter
/// outra apresentação em outro formulário.
/// </summary>
public sealed record ApresentacaoDoCampo
{
    /// <exception cref="ArgumentOutOfRangeException">O tipo de campo não é membro do enum, como num cast de inteiro.</exception>
    public ApresentacaoDoCampo(string rotulo, TipoRenderizacao tipoRenderizacao, string? ajuda = null, bool pedirConfirmacao = false)
    {
        if (!Enum.IsDefined(tipoRenderizacao))
        {
            throw new ArgumentOutOfRangeException(nameof(tipoRenderizacao), tipoRenderizacao, "O tipo de campo não é membro do enum de tipos de renderização.");
        }

        Rotulo = rotulo;
        TipoRenderizacao = tipoRenderizacao;
        Ajuda = ajuda;
        PedirConfirmacao = pedirConfirmacao;
    }

    public string Rotulo { get; }

    /// <summary>Como o campo é renderizado.</summary>
    public TipoRenderizacao TipoRenderizacao { get; }

    /// <summary>O texto de apoio exibido junto do campo.</summary>
    public string? Ajuda { get; }

    /// <summary>Se o formulário pede a resposta duas vezes para conferir a digitação.</summary>
    public bool PedirConfirmacao { get; }

    /// <summary>A apresentação na forma em que o item a guarda: rótulo aparado e ajuda, quando há, aparada.</summary>
    public ApresentacaoDoCampo NaFormaGravada() =>
        new((Rotulo ?? string.Empty).Trim(), TipoRenderizacao, FormaDoItem.TextoOpcional(Ajuda), PedirConfirmacao);

    /// <summary>A ajuda, quando há, cabe no limite. O rótulo é conferido com o código e a ordem do item.</summary>
    public IEnumerable<FieldError> ConferirAjuda()
    {
        if (FormaDoItem.TextoOpcional(Ajuda) is { Length: > FormaDoItem.AjudaMaxLength })
        {
            yield return new("ajuda", new DomainError(
                ItemFormularioErrorCodes.AjudaTamanho, $"A ajuda do campo tem no máximo {FormaDoItem.AjudaMaxLength} caracteres."));
        }
    }
}
