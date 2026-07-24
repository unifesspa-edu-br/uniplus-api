namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

public sealed class DefinirRegrasDerivacaoCommandValidator : AbstractValidator<DefinirRegrasDerivacaoCommand>
{
    public DefinirRegrasDerivacaoCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        // Lista obrigatória, mas pode ser vazia — vazia zera as regras de derivação do processo.
        RuleFor(x => x.Configuracoes)
            .NotNull()
            .WithMessage("Lista de configurações de derivação é obrigatória (pode ser vazia).");

        RuleForEach(x => x.Configuracoes)
            .NotNull()
            .WithMessage("Item de configuração de derivação não pode ser nulo.");

        RuleForEach(x => x.Configuracoes).ChildRules(config =>
        {
            config.RuleFor(c => c.CodigoFato)
                .NotEmpty()
                .WithMessage("O código do fato derivado é obrigatório.");

            config.RuleFor(c => c.Regras)
                .NotEmpty()
                .WithMessage("A derivação de um fato precisa de ao menos uma regra.");

            config.RuleForEach(c => c.Regras)
                .NotNull()
                .WithMessage("Item de regra de derivação não pode ser nulo.");

            config.RuleForEach(c => c.Regras).ChildRules(regra =>
            {
                regra.RuleFor(r => r.Ordem)
                    .GreaterThanOrEqualTo(0)
                    .WithMessage("A ordem da regra não pode ser negativa.");

                regra.RuleFor(r => r.Contribui)
                    .NotEmpty()
                    .WithMessage("Uma regra de derivação precisa contribuir um código.");

                // Regra âncora (incondicional) tem Quando null. Uma lista externa vazia, uma
                // cláusula interna vazia ou uma condição nula deixariam a semântica DNF ambígua ou
                // fariam o handler desreferenciar um item nulo — a âncora é representada por null.
                regra.RuleFor(r => r.Quando)
                    .Must(quando => quando is null
                        || (quando.Count > 0 && quando.All(static clausula =>
                            clausula is { Count: > 0 } && clausula.All(static condicao => condicao is not null))))
                    .WithMessage("O predicado 'quando', quando presente, não pode ser uma lista vazia, conter "
                        + "cláusulas vazias ou condições nulas — uma regra incondicional (âncora) tem 'quando' null.");
            });
        });
    }
}
