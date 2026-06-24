namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PesosAreaEnem;

using FluentValidation;

public sealed class AtualizarPesoAreaEnemCommandValidator
    : AbstractValidator<AtualizarPesoAreaEnemCommand>
{
    public AtualizarPesoAreaEnemCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id da linha de pesos do ENEM é obrigatório.");

        RuleFor(x => x.PesoRedacao).GreaterThanOrEqualTo(0).WithMessage("O peso de redação não pode ser negativo.");
        RuleFor(x => x.PesoCienciasNatureza).GreaterThanOrEqualTo(0).WithMessage("O peso de ciências da natureza não pode ser negativo.");
        RuleFor(x => x.PesoCienciasHumanas).GreaterThanOrEqualTo(0).WithMessage("O peso de ciências humanas não pode ser negativo.");
        RuleFor(x => x.PesoLinguagens).GreaterThanOrEqualTo(0).WithMessage("O peso de linguagens e códigos não pode ser negativo.");
        RuleFor(x => x.PesoMatematica).GreaterThanOrEqualTo(0).WithMessage("O peso de matemática não pode ser negativo.");

        RuleFor(x => x.CorteRedacao!.Value)
            .GreaterThanOrEqualTo(0).WithMessage("Corte de redação não pode ser negativo.")
            .When(x => x.CorteRedacao.HasValue);

        RuleFor(x => x.BaseLegal)
            .NotEmpty().WithMessage("Base legal é obrigatória.")
            .MaximumLength(500).WithMessage("Base legal deve ter no máximo 500 caracteres.");
    }
}
