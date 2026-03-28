namespace Unifesspa.UniPlus.Ingresso.Application.Commands.Chamadas;

using FluentValidation;

public sealed class CriarChamadaCommandValidator : AbstractValidator<CriarChamadaCommand>
{
    public CriarChamadaCommandValidator()
    {
        RuleFor(x => x.EditalId)
            .NotEmpty()
            .WithMessage("Identificador do edital é obrigatório.");

        RuleFor(x => x.DataPublicacao)
            .NotEmpty()
            .WithMessage("Data de publicação é obrigatória.");

        RuleFor(x => x.PrazoManifestacao)
            .NotEmpty()
            .WithMessage("Prazo de manifestação é obrigatório.")
            .GreaterThan(x => x.DataPublicacao)
            .WithMessage("Prazo de manifestação deve ser posterior à data de publicação.");
    }
}
