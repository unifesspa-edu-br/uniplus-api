namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>Confere o identificador da rota, que não chega ao domínio.</summary>
public sealed class DefinirTaxaInscricaoCommandValidator : AbstractValidator<DefinirTaxaInscricaoCommand>
{
    public DefinirTaxaInscricaoCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

    }
}
