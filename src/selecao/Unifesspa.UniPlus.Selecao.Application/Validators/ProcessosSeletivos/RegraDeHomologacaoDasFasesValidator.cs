namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>
/// Validação de <b>forma</b> da regra de homologação das inscrições que cada fase do cronograma declara: o item nulo das
/// listas, que o <c>ChildRules</c> não alcança e que a montagem da regra desreferenciaria. A fase sem a regra não é
/// validada, porque não homologa. Tipo, edição do exame, motivo, alternativa sem condição e enquadramento são recusas do
/// domínio e da montagem, acumuladas por campo (ADR-0125).
/// </summary>
public sealed class RegraDeHomologacaoDasFasesValidator : AbstractValidator<DefinirCronogramaFasesCommand>
{
    public RegraDeHomologacaoDasFasesValidator() =>
        RuleForEach(x => x.Fases).ChildRules(fase => fase.When(f => f.RegraDeHomologacao is not null, () =>
        {
            fase.RuleForEach(f => f.RegraDeHomologacao!.Alternativas)
                .NotNull()
                .WithMessage("Item de alternativa da regra de homologação não pode ser nulo.");

            fase.RuleForEach(f => f.RegraDeHomologacao!.Alternativas).ChildRules(alternativa =>
                alternativa.RuleForEach(a => a.Condicoes)
                    .NotNull()
                    .WithMessage("Item de condição da regra de homologação não pode ser nulo."));
        }));
}
