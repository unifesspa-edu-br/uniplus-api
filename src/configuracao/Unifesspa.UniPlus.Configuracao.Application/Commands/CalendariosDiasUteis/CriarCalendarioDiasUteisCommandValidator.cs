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

        // NotNull por item ANTES do ChildRules: sem isso, um elemento nulo (ex. JSON
        // "diasNaoUteis":[null]) passa incólume pelas regras — RuleForEach roda
        // ChildRules contra um objeto raiz nulo sem produzir falha — e o handler
        // desreferencia o item nulo ao montar DiaNaoUtilCriacao (500 em vez de 422).
        RuleForEach(x => x.DiasNaoUteis)
            .NotNull().WithMessage("Item de dia não útil não pode ser nulo.");

        RuleForEach(x => x.DiasNaoUteis).Where(d => d is not null).ChildRules(dia =>
        {
            dia.RuleFor(d => d.Abrangencia).NotEmpty().WithMessage("Abrangência é obrigatória.");
            dia.RuleFor(d => d.Descricao)
                .NotEmpty().WithMessage("Descrição é obrigatória.")
                .MaximumLength(200).WithMessage("Descrição deve ter no máximo 200 caracteres.");
        });
    }
}
