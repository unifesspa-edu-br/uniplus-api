namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TermosConsentimento;

using FluentValidation;

public sealed class CriarTermoConsentimentoCommandValidator : AbstractValidator<CriarTermoConsentimentoCommand>
{
    public CriarTermoConsentimentoCommandValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().WithMessage("Nome do termo é obrigatório.");
    }
}
