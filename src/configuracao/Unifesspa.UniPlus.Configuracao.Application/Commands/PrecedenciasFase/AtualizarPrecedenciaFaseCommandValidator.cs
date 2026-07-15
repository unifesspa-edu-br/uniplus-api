namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PrecedenciasFase;

using FluentValidation;

/// <summary>Antecipa (422) a obrigatoriedade do Id.</summary>
public sealed class AtualizarPrecedenciaFaseCommandValidator : AbstractValidator<AtualizarPrecedenciaFaseCommand>
{
    public AtualizarPrecedenciaFaseCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id da aresta de precedência é obrigatório.");
    }
}
