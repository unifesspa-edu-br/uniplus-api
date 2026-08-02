namespace Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;

using FluentValidation;

public sealed class CriarCalendarioDiasUteisCommandValidator : AbstractValidator<CriarCalendarioDiasUteisCommand>
{
    public CriarCalendarioDiasUteisCommandValidator()
    {
        RuleFor(x => x.VersaoDataset)
            .NotEmpty().WithMessage("Versão do dataset é obrigatória.")
            .MaximumLength(60).WithMessage("Versão do dataset deve ter no máximo 60 caracteres.");

        RuleFor(x => x.DiasNaoUteis)
            .NotEmpty().WithMessage("O dataset precisa de ao menos um dia não útil.");

        RuleForEach(x => x.DiasNaoUteis).ChildRules(dia =>
        {
            dia.RuleFor(d => d.Abrangencia).NotEmpty().WithMessage("Abrangência é obrigatória.");
            dia.RuleFor(d => d.Descricao)
                .NotEmpty().WithMessage("Descrição é obrigatória.")
                .MaximumLength(200).WithMessage("Descrição deve ter no máximo 200 caracteres.");
        });
    }
}
