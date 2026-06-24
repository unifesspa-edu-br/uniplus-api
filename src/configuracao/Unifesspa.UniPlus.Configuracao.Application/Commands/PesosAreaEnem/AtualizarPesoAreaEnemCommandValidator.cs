namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PesosAreaEnem;

using FluentValidation;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;

public sealed class AtualizarPesoAreaEnemCommandValidator
    : AbstractValidator<AtualizarPesoAreaEnemCommand>
{
    public AtualizarPesoAreaEnemCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id da linha de pesos do ENEM é obrigatório.");

        RuleFor(x => x.PesoRedacao).InclusiveBetween(0m, PesoAreaEnem.PesoMaximo).WithMessage($"O peso de redação deve estar entre 0 e {PesoAreaEnem.PesoMaximo}.");
        RuleFor(x => x.PesoCienciasNatureza).InclusiveBetween(0m, PesoAreaEnem.PesoMaximo).WithMessage($"O peso de ciências da natureza deve estar entre 0 e {PesoAreaEnem.PesoMaximo}.");
        RuleFor(x => x.PesoCienciasHumanas).InclusiveBetween(0m, PesoAreaEnem.PesoMaximo).WithMessage($"O peso de ciências humanas deve estar entre 0 e {PesoAreaEnem.PesoMaximo}.");
        RuleFor(x => x.PesoLinguagens).InclusiveBetween(0m, PesoAreaEnem.PesoMaximo).WithMessage($"O peso de linguagens e códigos deve estar entre 0 e {PesoAreaEnem.PesoMaximo}.");
        RuleFor(x => x.PesoMatematica).InclusiveBetween(0m, PesoAreaEnem.PesoMaximo).WithMessage($"O peso de matemática deve estar entre 0 e {PesoAreaEnem.PesoMaximo}.");

        RuleFor(x => x.CorteRedacao)
            .InclusiveBetween(0m, PesoAreaEnem.CorteRedacaoMaximo).WithMessage($"Corte de redação deve estar entre 0 e {PesoAreaEnem.CorteRedacaoMaximo}.");

        RuleFor(x => x.BaseLegal)
            .NotEmpty().WithMessage("Base legal é obrigatória.")
            .MaximumLength(500).WithMessage("Base legal deve ter no máximo 500 caracteres.");
    }
}
