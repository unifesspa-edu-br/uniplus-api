namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>Confere os dados informados e o limite operacional das regras de comprovação.</summary>
public sealed class DefinirDocumentosExigidosCommandValidator : AbstractValidator<DefinirDocumentosExigidosCommand>
{
    // Limita a profundidade para manter a validação de pedidos dentro da capacidade operacional.
    private const int MaxProfundidadeDasRegras = 20;

    public DefinirDocumentosExigidosCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");
        RuleFor(x => x.Raizes)
            .NotNull()
            .WithMessage("A lista de regras de comprovação não pode ser nula.");
        RegrasDeComprovacao();
    }

    private void RegrasDeComprovacao()
    {
        RuleForEach(x => x.Raizes)
            .NotNull()
            .WithMessage("Uma regra de comprovação não pode ser nula.")
            .SetValidator(new RegraDeComprovacaoInformadaValidator());

        RuleFor(x => x.Raizes)
            .Must(static regras => ProfundidadeMaxima(regras, 1) <= MaxProfundidadeDasRegras)
            .When(static x => x.Raizes is { Count: > 0 })
            .WithMessage($"As regras de comprovação excedem a profundidade operacional máxima ({MaxProfundidadeDasRegras}).");
    }

    private static int ProfundidadeMaxima(IReadOnlyList<RegraDeComprovacaoInformada>? regras, int profundidadeAtual) =>
        regras is not { Count: > 0 }
            ? profundidadeAtual - 1
            : regras.Max(regra => ProfundidadeMaxima(regra.Itens, profundidadeAtual + 1));
}
