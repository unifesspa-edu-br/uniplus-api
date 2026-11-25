namespace Unifesspa.UniPlus.Selecao.Application.Validators.AtosDoProcesso;

using Commands.AtosDoProcesso;

using Domain.Entities;

using FluentValidation;

/// <summary>
/// Valida o formato do comando de publicação do ato avulso antes de qualquer conferência.
/// </summary>
public sealed class PublicarAtoAvulsoDoProcessoCommandValidator : DadosDoAtoPublicadoValidator<PublicarAtoAvulsoDoProcessoCommand>
{
    public PublicarAtoAvulsoDoProcessoCommandValidator()
    {
        RuleFor(x => x.TipoAtoCodigo)
            .NotEmpty()
            .WithMessage("Tipo do ato é obrigatório.")
            .MaximumLength(AtoDoProcesso.TipoCodigoMaxLength)
            .WithMessage($"Tipo do ato deve ter no máximo {AtoDoProcesso.TipoCodigoMaxLength} caracteres.");
    }
}
