namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FasesCanonicas;

using FluentValidation;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Antecipa (422) o domínio fechado do dono típico e os tamanhos na atualização. O
/// <c>Codigo</c> é imutável e não é campo do comando; por isso a coerência
/// <c>agrupa_etapas</c>/<c>permite_complementacao</c> (que depende do código) fica
/// no agregado, revalidada contra o código congelado da fase.
/// </summary>
public sealed class AtualizarFaseCanonicaCommandValidator : AbstractValidator<AtualizarFaseCanonicaCommand>
{
    public AtualizarFaseCanonicaCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id da fase é obrigatório.");

        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Nome da fase é obrigatório.")
            .MaximumLength(200).WithMessage("Nome da fase deve ter no máximo 200 caracteres.");

        RuleFor(x => x.Descricao)
            .MaximumLength(300).WithMessage("Descrição da fase deve ter no máximo 300 caracteres.")
            .When(x => x.Descricao is not null);

        RuleFor(x => x.DonoTipico)
            .NotEmpty().WithMessage("Dono típico da fase é obrigatório.");

        RuleFor(x => x.DonoTipico)
            .Must(DonosTipicos.EhValido)
            .WithMessage($"Dono típico deve ser um de: {string.Join(", ", DonosTipicos.TokensCanonicos)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.DonoTipico));

        RuleFor(x => x.BaseLegal)
            .MaximumLength(500).WithMessage("Base legal da fase deve ter no máximo 500 caracteres.")
            .When(x => x.BaseLegal is not null);
    }
}
