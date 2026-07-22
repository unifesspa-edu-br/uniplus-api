namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

public sealed class DefinirBonusRegionalCommandValidator : AbstractValidator<DefinirBonusRegionalCommand>
{
    public DefinirBonusRegionalCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        // RegraCodigo nulo = remover o bônus (toggle por ausência, INV-B5) —
        // as demais regras só se aplicam quando um bônus está sendo definido.
        RuleFor(x => x.RegraVersao)
            .NotEmpty()
            .When(x => x.RegraCodigo is not null)
            .WithMessage("Versão da regra de bônus é obrigatória quando RegraCodigo é informado.");

        RuleFor(x => x.Fator)
            .NotNull()
            .GreaterThan(0)
            .PrecisionScale(6, 4, ignoreTrailingZeros: false)
            .When(x => x.RegraCodigo is not null)
            .WithMessage("Fator do bônus deve ser maior que zero, com no máximo 4 casas decimais, quando RegraCodigo é informado.");

        RuleFor(x => x.Teto)
            .GreaterThan(0)
            .PrecisionScale(6, 4, ignoreTrailingZeros: false)
            .When(x => x.Teto.HasValue)
            .WithMessage("Teto do bônus, quando informado, deve ser maior que zero e ter no máximo 4 casas decimais.");

        // Alinhado a ConfiguracaoBonusRegionalConfiguration (varchar(200)/(500))
        // — sem o limite aqui, um valor mais longo passa a validação e só
        // falha em SaveChanges com erro de banco em vez de 422 (achado Codex).
        RuleFor(x => x.MunicipioConvenio)
            .MaximumLength(200)
            .WithMessage("Município do convênio deve ter no máximo 200 caracteres.");

        RuleFor(x => x.BaseLegal)
            .MaximumLength(500)
            .WithMessage("Base legal deve ter no máximo 500 caracteres.");
    }
}
