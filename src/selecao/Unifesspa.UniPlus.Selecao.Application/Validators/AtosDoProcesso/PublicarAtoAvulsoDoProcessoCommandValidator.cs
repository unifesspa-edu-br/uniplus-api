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
            .WithMessage($"Tipo do ato deve ter no máximo {AtoDoProcesso.TipoCodigoMaxLength} caracteres.")
            // Só a forma: a lista de tipos é cadastro, e conhecê-la seria ramificar por tipo (ADR-0103).
            .Matches("^[A-Z]+(_[A-Z]+)*$")
            .WithMessage("Tipo do ato deve estar em forma canônica (ex.: AVISO).");
    }
}
