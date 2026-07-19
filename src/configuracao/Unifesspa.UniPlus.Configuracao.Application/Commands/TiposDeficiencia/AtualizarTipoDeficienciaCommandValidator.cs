namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDeficiencia;

using FluentValidation;

public sealed class AtualizarTipoDeficienciaCommandValidator
    : AbstractValidator<AtualizarTipoDeficienciaCommand>
{
    public AtualizarTipoDeficienciaCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id do tipo de deficiência é obrigatório.");

        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Nome do tipo de deficiência é obrigatório.")
            .MaximumLength(200).WithMessage("Nome do tipo de deficiência deve ter no máximo 200 caracteres.");

        RuleFor(x => x.Descricao)
            .NotEmpty().WithMessage("Descrição do tipo de deficiência é obrigatória.")
            .MaximumLength(1000).WithMessage("Descrição do tipo de deficiência deve ter no máximo 1000 caracteres.");
    }
}
