namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

public sealed class DefinirClassificacaoCommandValidator : AbstractValidator<DefinirClassificacaoCommand>
{
    public DefinirClassificacaoCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        RuleFor(x => x.RegraCalculoCodigo)
            .NotEmpty()
            .WithMessage("Código da regra de cálculo é obrigatório.");

        RuleFor(x => x.RegraCalculoVersao)
            .NotEmpty()
            .WithMessage("Versão da regra de cálculo é obrigatória.");

        // RegraArredondamentoCodigo nulo é válido (INV-B8: classificação
        // importada dispensa precisão local) — a versão só é exigida quando o
        // código é informado.
        RuleFor(x => x.RegraArredondamentoVersao)
            .NotEmpty()
            .When(x => x.RegraArredondamentoCodigo is not null)
            .WithMessage("Versão da regra de arredondamento é obrigatória quando o código é informado.");

        RuleFor(x => x.CasasArredondamento)
            .GreaterThan(0)
            .When(x => x.CasasArredondamento.HasValue)
            .WithMessage("Casas de arredondamento, quando informadas, devem ser maiores que zero.");

        RuleFor(x => x.RegraOrdemAlocacaoCodigo)
            .NotEmpty()
            .WithMessage("Código da regra de ordem de alocação é obrigatório.");

        RuleFor(x => x.RegraOrdemAlocacaoVersao)
            .NotEmpty()
            .WithMessage("Versão da regra de ordem de alocação é obrigatória.");

        RuleFor(x => x.NOpcoesAlocacao)
            .InclusiveBetween(1, 2)
            .WithMessage("Número de opções de curso deve ser 1 ou 2 (RN04).");

        // RuleForEach por si só não falha sobre uma coleção nula (apenas não
        // itera) — sem esta regra, um payload malformado que omita o campo
        // chegaria ao handler como null e estouraria no foreach em vez de
        // devolver 400.
        RuleFor(x => x.RegrasEliminacao)
            .NotNull()
            .WithMessage("Lista de regras de eliminação é obrigatória (pode ser vazia).");

        RuleForEach(x => x.RegrasEliminacao)
            .NotNull()
            .WithMessage("Item de regra de eliminação não pode ser nulo.");

        RuleForEach(x => x.RegrasEliminacao).ChildRules(item =>
        {
            item.RuleFor(r => r.RegraCodigo)
                .NotEmpty()
                .WithMessage("Código da regra de eliminação é obrigatório.");

            item.RuleFor(r => r.RegraVersao)
                .NotEmpty()
                .WithMessage("Versão da regra de eliminação é obrigatória.");
        });
    }
}
