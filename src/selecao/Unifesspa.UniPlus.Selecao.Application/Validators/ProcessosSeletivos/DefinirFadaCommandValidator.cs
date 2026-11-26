namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>Confere o identificador da rota, que não chega ao domínio.</summary>
public sealed class DefinirFadaCommandValidator : AbstractValidator<DefinirFadaCommand>
{
    public DefinirFadaCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

    }
}
