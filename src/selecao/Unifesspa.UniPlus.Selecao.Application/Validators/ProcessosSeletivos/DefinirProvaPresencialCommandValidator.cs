namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

public sealed class DefinirProvaPresencialCommandValidator : AbstractValidator<DefinirProvaPresencialCommand>
{
    public DefinirProvaPresencialCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId).NotEmpty().WithMessage("O processo seletivo é obrigatório.");
        RuleFor(x => x.Municipios).NotNull().WithMessage("A lista de municípios é obrigatória.");
    }
}
