namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PesosAreaEnem;

using FluentValidation;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
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

        RuleFor(x => x.PesoRedacao).InclusiveBetween(0m, PesoAreaEnem.PesoMaximo).WithMessage($"O peso de redação deve estar entre 0 e {PesoAreaEnem.PesoMaximo}.");
        RuleFor(x => x.PesoCienciasNatureza).InclusiveBetween(0m, PesoAreaEnem.PesoMaximo).WithMessage($"O peso de ciências da natureza deve estar entre 0 e {PesoAreaEnem.PesoMaximo}.");
        RuleFor(x => x.PesoCienciasHumanas).InclusiveBetween(0m, PesoAreaEnem.PesoMaximo).WithMessage($"O peso de ciências humanas deve estar entre 0 e {PesoAreaEnem.PesoMaximo}.");
        RuleFor(x => x.PesoLinguagens).InclusiveBetween(0m, PesoAreaEnem.PesoMaximo).WithMessage($"O peso de linguagens e códigos deve estar entre 0 e {PesoAreaEnem.PesoMaximo}.");
        RuleFor(x => x.PesoMatematica).InclusiveBetween(0m, PesoAreaEnem.PesoMaximo).WithMessage($"O peso de matemática deve estar entre 0 e {PesoAreaEnem.PesoMaximo}.");

        RuleFor(x => x.CorteRedacao)
            .InclusiveBetween(0m, PesoAreaEnem.CorteRedacaoMaximo).WithMessage($"Corte de redação deve estar entre 0 e {PesoAreaEnem.CorteRedacaoMaximo}.")
            .When(x => x.CorteRedacao.HasValue);

        RuleFor(x => x.BaseLegal)
            .NotEmpty().WithMessage("Base legal é obrigatória.")
            .MaximumLength(500).WithMessage("Base legal deve ter no máximo 500 caracteres.");
    }
}
