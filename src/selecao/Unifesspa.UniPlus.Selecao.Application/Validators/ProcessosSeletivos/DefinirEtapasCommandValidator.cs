namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using FluentValidation;

using Commands.ProcessosSeletivos;
using Domain.Enums;

public sealed class DefinirEtapasCommandValidator : AbstractValidator<DefinirEtapasCommand>
{
    public DefinirEtapasCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        // Story #851 §3.5: lista vazia passa a ser um payload válido (processo sem
        // prova, ex. SiSU) — só a nulidade do array em si é recusada aqui.
        RuleFor(x => x.Etapas)
            .NotNull()
            .WithMessage("O campo Etapas é obrigatório (pode ser uma lista vazia).");

        // Rejeita item nulo no array (ex.: `[null]`) antes das regras de campo —
        // sem isso o ChildRules não gera falha para o elemento nulo e o handler
        // desreferenciaria a etapa, estourando como 500.
        RuleForEach(x => x.Etapas)
            .NotNull()
            .WithMessage("Item de etapa não pode ser nulo.");

        RuleForEach(x => x.Etapas).ChildRules(etapa =>
        {
            etapa.RuleFor(e => e.Nome)
                .NotEmpty()
                .WithMessage("Nome da etapa é obrigatório.")
                .MaximumLength(300)
                .WithMessage("Nome da etapa deve ter no máximo 300 caracteres.");

            etapa.RuleFor(e => e.Carater)
                .NotEqual(CaraterEtapa.Nenhum)
                .WithMessage("Caráter da etapa é obrigatório.")
                .IsInEnum()
                .WithMessage("Caráter da etapa inválido.");

            // O peso é persistido como numeric(18,4). Sem o limite de escala,
            // um valor positivo com mais de 4 casas (ex.: 0.00001) passa aqui e
            // no guard de divisor-zero do domínio, mas o banco arredonda para
            // 0.0000 — depois do reload a etapa continua "compondo nota"
            // (Peso.HasValue) enquanto CalcularDivisorMedia() soma zero.
            etapa.RuleFor(e => e.Peso)
                .GreaterThan(0)
                .PrecisionScale(18, 4, ignoreTrailingZeros: false)
                .When(e => e.Peso.HasValue)
                .WithMessage("Peso da etapa deve ser maior que zero e ter no máximo 4 casas decimais.");

            // NotaMinima também é persistida como numeric(18,4) e controla o
            // corte de eliminação — mesma proteção de escala do peso, para o
            // banco não arredondar o limiar silenciosamente.
            etapa.RuleFor(e => e.NotaMinima)
                .GreaterThanOrEqualTo(0)
                .PrecisionScale(18, 4, ignoreTrailingZeros: false)
                .When(e => e.NotaMinima.HasValue)
                .WithMessage("Nota mínima deve ser não negativa e ter no máximo 4 casas decimais.");

            etapa.RuleFor(e => e.Ordem)
                .GreaterThan(0)
                .When(e => e.Ordem.HasValue)
                .WithMessage("Ordem da etapa deve ser maior que zero, quando informada.");
        });
    }
}
