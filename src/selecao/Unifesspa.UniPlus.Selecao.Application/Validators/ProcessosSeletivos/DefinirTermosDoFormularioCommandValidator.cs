namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>
/// Checa só a forma do <c>ProcessoSeletivoId</c>, identificador de rota sem equivalente no
/// agregado. A forma de cada termo tem equivalente de domínio (ADR-0125) e é conferida no handler.
/// </summary>
public sealed class DefinirTermosDoFormularioCommandValidator : AbstractValidator<DefinirTermosDoFormularioCommand>
{
    public DefinirTermosDoFormularioCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");
    }
}
