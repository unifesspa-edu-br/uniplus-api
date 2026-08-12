namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using Domain.Entities;

using FluentValidation;

public sealed class DefinirTaxaInscricaoCommandValidator : AbstractValidator<DefinirTaxaInscricaoCommand>
{
    public DefinirTaxaInscricaoCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        // Alinhado a ConfiguracaoTaxaInscricaoConfiguration (numeric(12,2)) — sem o limite aqui,
        // um valor mais preciso passa a validação e só falha em SaveChanges com erro de banco
        // em vez de 422.
        RuleFor(x => x.Valor)
            .GreaterThan(0)
            .PrecisionScale(ConfiguracaoTaxaInscricao.ValorPrecisao, ConfiguracaoTaxaInscricao.ValorEscala, ignoreTrailingZeros: false)
            .When(x => x.Valor.HasValue)
            .WithMessage(
                $"Valor da taxa, quando informado, deve ser maior que zero e ter no máximo " +
                $"{ConfiguracaoTaxaInscricao.ValorEscala} casas decimais.");
    }
}
