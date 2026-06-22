namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ReferenciasReservaDemografica;

using FluentValidation;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

public sealed class CriarReferenciaReservaDemograficaCommandValidator
    : AbstractValidator<CriarReferenciaReservaDemograficaCommand>
{
    public CriarReferenciaReservaDemograficaCommandValidator()
    {
        RuleFor(x => x.CensoReferencia)
            .NotEmpty().WithMessage("Censo de referência é obrigatório.")
            .MaximumLength(20).WithMessage("Censo de referência deve ter no máximo 20 caracteres.");

        RuleFor(x => x.PpiPercentual)
            .InclusiveBetween(Percentual.Minimo, Percentual.Maximo)
            .WithMessage("Percentual de PPI deve estar entre 0 e 100.");

        RuleFor(x => x.QuilombolaPercentual)
            .InclusiveBetween(Percentual.Minimo, Percentual.Maximo)
            .WithMessage("Percentual de quilombolas deve estar entre 0 e 100.");

        RuleFor(x => x.PcdPercentual)
            .InclusiveBetween(Percentual.Minimo, Percentual.Maximo)
            .WithMessage("Percentual de PcD deve estar entre 0 e 100.");

        RuleFor(x => x.BaseLegal)
            .NotEmpty().WithMessage("Base legal é obrigatória.")
            .MaximumLength(500).WithMessage("Base legal deve ter no máximo 500 caracteres.");
    }
}
