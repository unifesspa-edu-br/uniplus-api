namespace Unifesspa.UniPlus.Selecao.Application.Validators.AtosDoProcesso;

using Commands.AtosDoProcesso;

using FluentValidation;

/// <summary>
/// Valida o formato do comando de publicação de um item do edital antes de qualquer conferência.
/// </summary>
public sealed class PublicarProdutoDoEditalCommandValidator : DadosDoAtoPublicadoValidator<PublicarProdutoDoEditalCommand>
{
    public PublicarProdutoDoEditalCommandValidator()
    {
        RuleFor(x => x.ProdutoDoEditalId)
            .NotEmpty()
            .WithMessage("Id do item do edital é obrigatório.");
    }
}
