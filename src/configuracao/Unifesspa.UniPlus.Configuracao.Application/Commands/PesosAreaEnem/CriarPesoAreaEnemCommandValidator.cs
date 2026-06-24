namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PesosAreaEnem;

using FluentValidation;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

public sealed class CriarPesoAreaEnemCommandValidator
    : AbstractValidator<CriarPesoAreaEnemCommand>
{
    public CriarPesoAreaEnemCommandValidator()
    {
        RuleFor(x => x.Resolucao)
            .NotEmpty().WithMessage("Resolução é obrigatória.")
            .MaximumLength(40).WithMessage("Resolução deve ter no máximo 40 caracteres.");

        RuleFor(x => x.GrupoCurso)
            .Must(GrupoCurso.EhValido)
            .WithMessage($"Grupo de curso deve ser um de: {string.Join(", ", GrupoCurso.Valores)}.");

        RuleFor(x => x.PesoRedacao).GreaterThanOrEqualTo(0).WithMessage("O peso de redação não pode ser negativo.");
        RuleFor(x => x.PesoCienciasNatureza).GreaterThanOrEqualTo(0).WithMessage("O peso de ciências da natureza não pode ser negativo.");
        RuleFor(x => x.PesoCienciasHumanas).GreaterThanOrEqualTo(0).WithMessage("O peso de ciências humanas não pode ser negativo.");
        RuleFor(x => x.PesoLinguagens).GreaterThanOrEqualTo(0).WithMessage("O peso de linguagens e códigos não pode ser negativo.");
        RuleFor(x => x.PesoMatematica).GreaterThanOrEqualTo(0).WithMessage("O peso de matemática não pode ser negativo.");

        RuleFor(x => x.CorteRedacao)
            .GreaterThanOrEqualTo(0m).WithMessage("Corte de redação não pode ser negativo.")
            .When(x => x.CorteRedacao.HasValue);

        RuleFor(x => x.BaseLegal)
            .NotEmpty().WithMessage("Base legal é obrigatória.")
            .MaximumLength(500).WithMessage("Base legal deve ter no máximo 500 caracteres.");
    }
}
