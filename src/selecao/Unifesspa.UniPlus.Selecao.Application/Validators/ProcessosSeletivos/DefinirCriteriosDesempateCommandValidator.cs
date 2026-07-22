namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

public sealed class DefinirCriteriosDesempateCommandValidator : AbstractValidator<DefinirCriteriosDesempateCommand>
{
    public DefinirCriteriosDesempateCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        // Critérios de desempate são opcionais (0..*) — lista vazia é válida
        // (remove todos os critérios), diferente de DefinirEtapas/DefinirDistribuicaoVagas.
        // RuleForEach por si só não falha sobre uma coleção NULA (apenas não
        // itera) — sem a regra a seguir, um payload malformado chegaria ao
        // handler como null e estouraria no foreach em vez de devolver 400.
        RuleFor(x => x.Criterios)
            .NotNull()
            .WithMessage("Lista de critérios de desempate é obrigatória (pode ser vazia).");

        RuleForEach(x => x.Criterios)
            .NotNull()
            .WithMessage("Item de critério de desempate não pode ser nulo.");

        RuleForEach(x => x.Criterios).ChildRules(item =>
        {
            item.RuleFor(c => c.Ordem)
                .GreaterThan(0)
                .WithMessage("Ordem do critério de desempate deve ser maior que zero.");

            item.RuleFor(c => c.RegraCodigo)
                .NotEmpty()
                .WithMessage("Código da regra de desempate é obrigatório.");

            item.RuleFor(c => c.RegraVersao)
                .NotEmpty()
                .WithMessage("Versão da regra de desempate é obrigatória.");
        });
    }
}
