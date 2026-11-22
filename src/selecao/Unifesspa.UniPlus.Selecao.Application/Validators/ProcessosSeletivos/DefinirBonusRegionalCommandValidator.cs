namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>Confere o identificador da rota, que não chega ao domínio.</summary>
public sealed class DefinirBonusRegionalCommandValidator : AbstractValidator<DefinirBonusRegionalCommand>
{
    public DefinirBonusRegionalCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

    }
}
