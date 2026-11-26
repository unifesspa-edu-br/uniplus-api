namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

public sealed class DefinirManifestacaoDeInteresseCommandValidator : AbstractValidator<DefinirManifestacaoDeInteresseCommand>
{
    public DefinirManifestacaoDeInteresseCommandValidator() =>
        RuleFor(x => x.ProcessoSeletivoId).NotEmpty().WithMessage("O processo seletivo é obrigatório.");
}
