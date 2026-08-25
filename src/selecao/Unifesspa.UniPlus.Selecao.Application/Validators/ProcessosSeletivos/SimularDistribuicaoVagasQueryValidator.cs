namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using FluentValidation;

using Queries.ProcessosSeletivos;

/// <summary>
/// Mesmas regras de shape do <see cref="DefinirDistribuicaoVagasCommandValidator"/>
/// (issue #1282): a simulação aceita o mesmo payload que o PUT que persiste
/// e precisa recusar o mesmo malformado antes de chegar ao handler.
/// </summary>
public sealed class SimularDistribuicaoVagasQueryValidator : AbstractValidator<SimularDistribuicaoVagasQuery>
{
    public SimularDistribuicaoVagasQueryValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        RuleFor(x => x.DistribuicaoVagas)
            .NotEmpty()
            .WithMessage("Informe ao menos uma distribuição de vagas para simular.");

        RuleForEach(x => x.DistribuicaoVagas)
            .NotNull()
            .WithMessage("Item de distribuição de vagas não pode ser nulo.");

        RuleForEach(x => x.DistribuicaoVagas).SetValidator(new ConfiguracaoDistribuicaoVagasInputValidator());
    }
}
